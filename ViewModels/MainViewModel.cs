using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.IO;
using System.Text;
using YureteruWPF.Models;
using YureteruWPF.Services;
using YureteruWPF.Utilities;

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

    // --- New: background parsing / producer-consumer queue to keep UI thread smooth ---
    private readonly ConcurrentQueue<string> _rawQueue = new();
    private CancellationTokenSource? _processingCts;
    private Task? _processingTask;
    private readonly DispatcherTimer _uiUpdateTimer;
    private double _latestIntensity = 0.0; // latest received intensity (background-updated)
    // -------------------------------------------------------------------------------

    public void Dispose()
    {
        // stop timers
        _clockTimer.Stop();
        _graphTimer.Stop();
        _uiUpdateTimer.Stop();

        // stop processing loop
        _processingCts?.Cancel();
        try { _processingTask?.Wait(500); } catch { /* ignore */ }

        _serialService.DataReceived -= OnSerialDataEnqueue;
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
        _serialService.DataReceived += OnSerialDataEnqueue;
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

        // UI update timer (throttle UI intensity updates to 10Hz)
        _uiUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _uiUpdateTimer.Tick += (s, e) =>
        {
            // Smoothly push latest intensity to UI thread at limited rate
            CurrentIntensity = _latestIntensity;
            // Update IsRecordingEvent from recording service (safe read)
            IsRecordingEvent = _eventRecordingService.IsRecordingEvent;
        };
        _uiUpdateTimer.Start();

        // Start background processing loop for incoming serial lines
        _processingCts = new CancellationTokenSource();
        _processingTask = Task.Run(() => ProcessQueueLoopAsync(_processingCts.Token));

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

    private void OnSerialDataEnqueue(object? sender, string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        _rawQueue.Enqueue(line);
    }

    /// <summary>
    /// Background loop that parses raw serial lines and performs non-UI processing.
    /// Parsed acceleration data is added to _accBuffer (thread-safe via lock).
    /// Parsed intensity is stored to _latestIntensity and passed to recording service.
    /// UI updates are throttled and applied on the Dispatcher via _uiUpdateTimer.
    /// </summary>
    private async Task ProcessQueueLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (_rawQueue.TryDequeue(out var line))
                {
                    try
                    {
                        var parsed = _dataParser.ParseLine(line);
                        if (parsed == null) continue;

                        if (parsed is AccelerationData accData)
                        {
                            // Calculate additional properties
                            accData.VectorMagnitude = SeismicCalculations.CalculateVectorMagnitude(accData.X, accData.Y, accData.Z);
                            accData.Gal = SeismicCalculations.ConvertToGal(accData.VectorMagnitude);

                            // LPGM Processing (background)
                            var filteredAcc = _lpgmFilter.Process(accData.Gal);
                            var velocity = Math.Abs(_lpgmIntegrator.Process(filteredAcc));

                            // Update current SVA and class in background
                            CurrentSva = velocity;
                            CurrentLpgmClass = SeismicCalculations.CalculateLpgmClass(CurrentSva);

                            // Add to buffer (will be consumed by UI graph timer)
                            lock (_bufferLock)
                            {
                                _accBuffer.Add(accData);
                            }
                        }
                        else if (parsed is ParsedData pData && pData.Type == DataType.Intensity)
                        {
                            var value = (double)(pData.Value ?? 0.0);

                            // Update latest intensity (background) and notify recording service
                            _latestIntensity = value;

                            // Audio alert check & recording processing (non-UI)
                            _audioAlertService.CheckAndPlayAlert(value);
                            _eventRecordingService.ProcessIntensity(value, CurrentGal, CurrentLpgmClass, CurrentSva);
                        }
                    }
                    catch
                    {
                        // ignore parse or processing errors per-line
                    }

                    continue;
                }

                // If queue empty, wait a short while
                await Task.Delay(5, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
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

    partial void OnCurrentIntensityChanged(double value)
    {
        // Ensure derived bindings update when generated CurrentIntensity changes
        OnPropertyChanged(nameof(IsIntensityAtLeast2));
    }

    public bool IsIntensityAtLeast2 => CurrentIntensity >= 2.0;
}
