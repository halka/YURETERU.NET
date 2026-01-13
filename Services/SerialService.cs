using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace YureteruWPF.Services;

/// <summary>
/// Serial port communication service (ported from useSerial.js)
/// </summary>
public class SerialService : ISerialService, IDisposable
{
    private SerialPort? _serialPort;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isConnected;
    private bool _isMocking;

    public event EventHandler<string>? DataReceived;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsConnected => _isConnected;
    public bool IsMocking => _isMocking;

    public string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }

    public async Task<bool> ConnectAsync(string portName, int baudRate)
    {
        try
        {
            _serialPort = new SerialPort(portName, baudRate)
            {
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                ReadTimeout = 500,
                WriteTimeout = 500
            };

            _serialPort.Open();

            _isConnected = true;
            _isMocking = false;
            _cancellationTokenSource = new CancellationTokenSource();

            // Start dedicated reading loop
            _ = Task.Run(() => ReadLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);

            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Connection failed: {ex.Message}\n\nPlease ensure:\n- Device is connected\n- No other application is using the port\n- You have necessary permissions");
            return false;
        }
    }

    public void StartMock()
    {
        if (_isConnected) return;

        _isConnected = true;
        _isMocking = true;
        _cancellationTokenSource = new CancellationTokenSource();

        _ = Task.Run(() => MockReadLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
    }

    public async Task DisconnectAsync()
    {
        _cancellationTokenSource?.Cancel();
        _isConnected = false;
        _isMocking = false;

        if (_serialPort != null)
        {
            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }
            _serialPort.Dispose();
            _serialPort = null;
        }

        await Task.CompletedTask;
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        byte[] readBuffer = new byte[1024];

        while (!ct.IsCancellationRequested && _serialPort != null && _serialPort.IsOpen)
        {
            try
            {
                if (_serialPort.BytesToRead > 0)
                {
                    int count = _serialPort.Read(readBuffer, 0, readBuffer.Length);
                    if (count > 0)
                    {
                        string data = Encoding.ASCII.GetString(readBuffer, 0, count);
                        sb.Append(data);

                        string content = sb.ToString();
                        int nextLine;
                        int lastIndex = 0;

                        while ((nextLine = content.IndexOf('\n', lastIndex)) != -1)
                        {
                            string line = content.Substring(lastIndex, nextLine - lastIndex).Trim();
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                DataReceived?.Invoke(this, line);
                            }
                            lastIndex = nextLine + 1;
                        }

                        if (lastIndex > 0)
                        {
                            sb.Remove(0, lastIndex);
                        }
                    }
                }
                else
                {
                    await Task.Delay(10, ct);
                }
            }
            catch (TimeoutException) { }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                ErrorOccurred?.Invoke(this, $"Serial connection lost: {ex.Message}");
                _ = DisconnectAsync();
                break;
            }
        }
    }

    private async Task MockReadLoopAsync(CancellationToken cancellationToken)
    {
        string testFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_data.csv");
        // Fallback to project root if base directory doesn't have it (for dev)
        if (!File.Exists(testFilePath))
            testFilePath = "test_data.csv";

        var startTime = DateTime.Now;
        var random = new Random();
        string[]? fileLines = null;
        int currentLineIndex = 0;

        if (File.Exists(testFilePath))
        {
            try { fileLines = File.ReadAllLines(testFilePath); }
            catch { /* Ignore and fallback */ }
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            double x = 0, y = 0, z = 0;
            double intensity = 0;

            if (fileLines != null && fileLines.Length > 0)
            {
                // File-based playback
                string line = fileLines[currentLineIndex].Trim();
                currentLineIndex++;
                if (currentLineIndex >= fileLines.Length) currentLineIndex = 0;

                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                var parts = line.Split(',');
                if (parts.Length >= 3)
                {
                    double.TryParse(parts[0], out x);
                    double.TryParse(parts[1], out y);
                    double.TryParse(parts[2], out z);
                    if (parts.Length >= 4) double.TryParse(parts[3], out intensity);
                }
            }
            else
            {
                // Procedural Fallback
                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                if (elapsed < 10) { x = (random.NextDouble() - 0.5) * 0.02; y = (random.NextDouble() - 0.5) * 0.02; z = (random.NextDouble() - 0.5) * 0.02; }
                else if (elapsed < 15) { x = Math.Sin(elapsed * 50) * 0.1; y = Math.Cos(elapsed * 45) * 0.1; intensity = 0.5 + random.NextDouble() * 0.5; }
                else if (elapsed < 30) { x = Math.Sin(elapsed * 10) * 2.0; y = Math.Cos(elapsed * 12) * 1.5; z = Math.Sin(elapsed * 8) * 1.0; intensity = 3.5 + Math.Sin(elapsed) * 1.5; }
                else if (elapsed < 60) { x = Math.Sin(elapsed * 2 * Math.PI / 3.33) * 0.5; y = Math.Cos(elapsed * 2 * Math.PI / 3.33) * 0.4; intensity = 1.0 + random.NextDouble(); }
                else startTime = DateTime.Now;
            }

            string accLine = $"$XSACC,{x:F3},{y:F3},{z:F3}*00";
            string intLine = $"$XSINT,{intensity:F2}*00";

            DataReceived?.Invoke(this, accLine);
            DataReceived?.Invoke(this, intLine);

            await Task.Delay(100, cancellationToken);
        }
    }

    public void Dispose()
    {
        DisconnectAsync().Wait();
    }
}
