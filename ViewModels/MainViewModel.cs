using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using YureteruWPF.Models;
using YureteruWPF.Services;
using YureteruWPF.Utilities;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Threading.Tasks;

namespace YureteruWPF.ViewModels;

/// <summary>
/// Main ViewModel orchestrating the entire application
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private static readonly SKColor AxisXColor = SKColor.Parse("#FF3B30");
    private static readonly SKColor AxisYColor = SKColor.Parse("#007AFF");
    private static readonly SKColor AxisZColor = SKColor.Parse("#4CD964");
    private static readonly SKColor SeparatorColor = SKColor.Parse("#444444");
    public event Action? RequestSettingsWindow;
    public event Action<string, Action<string?>>? RequestSaveFile;

    private readonly ISerialService _serialService;
    private readonly IDataParser _dataParser;
    private readonly IAudioAlertService _audioAlertService;
    private readonly IEventRecordingService _eventRecordingService;

    private const int ClockIntervalMs = 43; // ~23Hz
    private const int GraphRefreshIntervalMs = 33; // ~30Hz

    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _graphTimer;
    private readonly object _bufferLock = new();

    public void Dispose()
    {
        _clockTimer.Stop();
        _graphTimer.Stop();
        _serialService.DataReceived -= OnDataReceived;
        _serialService.ErrorOccurred -= OnErrorOccurred;
        GC.SuppressFinalize(this);
    }

    [ObservableProperty]
    private ObservableCollection<AccelerationData> _dataPoints = new();

    [ObservableProperty]
    private AccelerationData _currentAcceleration = new();

    [ObservableProperty]
    private double _currentIntensity;

    [ObservableProperty]
    private double _currentGal;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isMocking;

    [ObservableProperty]
    private bool _isRecordingEvent;

    [ObservableProperty]
    private int _currentLpgmClass;

    [ObservableProperty]
    private double _currentSva; // cm/s

    private readonly SeismicCalculations.BiquadFilter _lpgmFilter = new();
    private readonly SeismicCalculations.Integrator _lpgmIntegrator = new(0.01); // 100Hz assumed


    [ObservableProperty]
    private DateTime _currentTime = DateTime.Now;

    // LiveCharts Properties
    public ISeries[] Series { get; set; }
    public Axis[] XAxes { get; set; }
    public Axis[] YAxes { get; set; }

    [ObservableProperty]
    private int _baudRate = 115200;

    [ObservableProperty]
    private string? _selectedPort;

    [ObservableProperty]
    private string? _statusMessage;

    public ObservableCollection<int> AvailableBaudRates { get; } = new()
    {
        9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600
    };

    public ObservableCollection<string> AvailablePorts { get; private set; } = new();

    public ObservableCollection<SeismicEvent> EventHistory => _eventRecordingService.EventHistory;

    private readonly ObservableCollection<AccelerationData> _accBuffer = new();

    public MainViewModel(
        ISerialService serialService,
        IDataParser dataParser,
        IAudioAlertService audioAlertService,
        IEventRecordingService eventRecordingService)
    {
        _serialService = serialService;
        _dataParser = dataParser;
        _audioAlertService = audioAlertService;
        _eventRecordingService = eventRecordingService;
        // Ensure startup is normal mode (no connection, no test)
        IsConnected = false;
        IsMocking = false;

        // Subscribe to serial service events
        _serialService.DataReceived += OnDataReceived;
        _serialService.ErrorOccurred += OnErrorOccurred;

        // Initialize LiveCharts
        Series = new ISeries[]
        {
            new LineSeries<AccelerationData>
            {
                Name = "X",
                Values = DataPoints,
                Mapping = (AccelerationData acc, int index) => new(index, acc.X),
                Stroke = new SolidColorPaint(AxisXColor) { StrokeThickness = 3 },
                Fill = new SolidColorPaint(AxisXColor.WithAlpha(30)),
                GeometrySize = 0,
                LineSmoothness = 0.5
            },
            new LineSeries<AccelerationData>
            {
                Name = "Y",
                Values = DataPoints,
                Mapping = (AccelerationData acc, int index) => new(index, acc.Y),
                Stroke = new SolidColorPaint(AxisYColor) { StrokeThickness = 3 },
                Fill = new SolidColorPaint(AxisYColor.WithAlpha(30)),
                GeometrySize = 0,
                LineSmoothness = 0.5
            },
            new LineSeries<AccelerationData>
            {
                Name = "Z",
                Values = DataPoints,
                Mapping = (AccelerationData acc, int index) => new(index, acc.Z),
                Stroke = new SolidColorPaint(AxisZColor) { StrokeThickness = 3 },
                Fill = new SolidColorPaint(AxisZColor.WithAlpha(30)),
                GeometrySize = 0,
                LineSmoothness = 0.5
            }
        };

        XAxes = new Axis[] { new Axis { IsVisible = false } };
        YAxes = new Axis[] { new Axis { SeparatorsPaint = new SolidColorPaint(SeparatorColor) { StrokeThickness = 0.5f } } };

        // Initialize LiveCharts
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ClockIntervalMs)
        };
        _clockTimer.Tick += (s, e) => CurrentTime = DateTime.Now;
        _clockTimer.Start();

        // Setup graph update timer (33ms ~30Hz)
        _graphTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(GraphRefreshIntervalMs)
        };
        _graphTimer.Tick += OnGraphTimerTick;
        _graphTimer.Start();

        RefreshPorts();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrEmpty(SelectedPort)) return;
        StatusMessage = "Connecting...";

        var success = await _serialService.ConnectAsync(SelectedPort, BaudRate);
        if (success)
        {
            IsConnected = true;
            IsMocking = false;
        }
    }

    [RelayCommand]
    private void StartMock()
    {
        StatusMessage = null;
        _serialService.StartMock();
        IsConnected = true;
        IsMocking = true;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        RefreshPorts();
        RequestSettingsWindow?.Invoke();
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _serialService.DisconnectAsync();
        IsConnected = false;
        IsMocking = false;
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        var ports = _serialService.GetAvailablePorts();
        AvailablePorts.Clear();
        foreach (var port in ports)
        {
            AvailablePorts.Add(port);
        }

        if (AvailablePorts.Count > 0 && SelectedPort == null)
        {
            SelectedPort = AvailablePorts[0];
        }
    }

    [RelayCommand]
    private void ExportHistory()
    {
        if (EventHistory.Count == 0) return;

        var defaultFilename = $"seismic_history_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        RequestSaveFile?.Invoke(defaultFilename, (filePath) =>
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    _eventRecordingService.ExportToCsv(filePath);
                    StatusMessage = $"History exported to {Path.GetFileName(filePath)}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        });
    }

    private void OnDataReceived(object? sender, string line)
    {
        var parsed = _dataParser.ParseLine(line);
        if (parsed == null) return;

        if (parsed is AccelerationData accData)
        {
            // Calculate additional properties
            accData.VectorMagnitude = SeismicCalculations.CalculateVectorMagnitude(accData.X, accData.Y, accData.Z);
            accData.Gal = SeismicCalculations.ConvertToGal(accData.VectorMagnitude);

            // LPGM Processing
            // 1. Filter Acceleration (Bandpass 1.6s - 7.8s)
            var filteredAcc = _lpgmFilter.Process(accData.Gal);
            
            // 2. Integrate to Velocity (cm/s)
            var velocity = Math.Abs(_lpgmIntegrator.Process(filteredAcc));

            // 3. Update Properties (Rolling Max for Sva approximation)
            // Ideally we should keep a rolling window for Sva, but for immediate feedback:
            CurrentSva = velocity; 
            CurrentLpgmClass = SeismicCalculations.CalculateLpgmClass(CurrentSva);

            // Add to buffer
            lock (_bufferLock)
            {
                _accBuffer.Add(accData);
            }
        }
        else if (parsed is ParsedData pData && pData.Type == DataType.Intensity)
        {
            var value = (double)(pData.Value ?? 0.0);
            
            App.Current.Dispatcher.Invoke(() =>
            {
                CurrentIntensity = value;

                // Check for audio alert
                _audioAlertService.CheckAndPlayAlert(value);

                // Process for event recording
                _eventRecordingService.ProcessIntensity(value, CurrentGal, CurrentLpgmClass, CurrentSva);
                IsRecordingEvent = _eventRecordingService.IsRecordingEvent;
            });
        }
    }

    private void OnGraphTimerTick(object? sender, EventArgs e)
    {
        lock (_bufferLock)
        {
            if (_accBuffer.Count > 0)
            {
                foreach (var data in _accBuffer)
                {
                    DataPoints.Add(data);
                }

                // Keep only last 50 points
                while (DataPoints.Count > 50)
                {
                    DataPoints.RemoveAt(0);
                }

                // Update current acceleration
                if (DataPoints.Count > 0)
                {
                    CurrentAcceleration = DataPoints[^1];
                    CurrentGal = CurrentAcceleration.Gal;
                }

                _accBuffer.Clear();
            }
        }
    }

    private void OnErrorOccurred(object? sender, string error)
    {
        StatusMessage = error;
        // Also show a message box for critical errors
        App.Current.Dispatcher.Invoke(() =>
        {
             System.Windows.MessageBox.Show(error, "Communication Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        });
    }
}
