using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Globalization;

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

    private static string? FindFileUpwards(string startDir, string fileName, int maxLevels = 6)
    {
        try
        {
            var dir = new DirectoryInfo(startDir);
            for (int i = 0; i <= maxLevels && dir != null; i++)
            {
                var candidate = Path.Combine(dir.FullName, fileName);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }
        catch { /* ignore */ }
        return null;
    }

    private async Task MockReadLoopAsync(CancellationToken cancellationToken)
    {
        // Try common locations: base dir, project root, working dir, and walk up
        string testFileName = "test_data.csv";
        string? testFilePath = null;

        var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory;
        // 1) base dir
        if (File.Exists(Path.Combine(baseDir, testFileName)))
            testFilePath = Path.Combine(baseDir, testFileName);
        // 2) current working directory
        if (testFilePath == null && File.Exists(Path.Combine(Environment.CurrentDirectory, testFileName)))
            testFilePath = Path.Combine(Environment.CurrentDirectory, testFileName);
        // 3) search upwards from base dir
        if (testFilePath == null)
            testFilePath = FindFileUpwards(baseDir, testFileName, maxLevels: 8);
        // 4) fallback to file name (relative)
        if (testFilePath == null && File.Exists(testFileName))
            testFilePath = Path.GetFullPath(testFileName);

        var startTime = DateTime.Now;
        var random = new Random();
        string[]? fileLines = null;
        int currentLineIndex = 0;

        if (!string.IsNullOrEmpty(testFilePath) && File.Exists(testFilePath))
        {
            try
            {
                fileLines = File.ReadAllLines(testFilePath);
            }
            catch { /* Ignore and fallback */ }
        }

        // Try to find first data line index for CSV with header/meta lines.
        int dataStartIndex = 0;
        if (fileLines != null && fileLines.Length > 0)
        {
            for (int i = 0; i < fileLines.Length; i++)
            {
                var line = fileLines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                // If line explicitly contains the header 'NS' or 'NS,EW' assume next lines are data
                if (line.StartsWith("NS", StringComparison.OrdinalIgnoreCase) || line.StartsWith("NS,EW", StringComparison.OrdinalIgnoreCase))
                {
                    dataStartIndex = i + 1;
                    break;
                }
                // Otherwise test whether the line contains at least three numeric tokens
                var tokens = line.Split(',');
                int numericCount = 0;
                foreach (var t in tokens)
                {
                    if (double.TryParse(t.Trim(), NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
                    {
                        numericCount++;
                        if (numericCount >= 3) break;
                    }
                }
                if (numericCount >= 3)
                {
                    dataStartIndex = i;
                    break;
                }
            }

            currentLineIndex = dataStartIndex;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            double x = 0, y = 0, z = 0;
            double intensity = 0;

            if (fileLines != null && fileLines.Length > dataStartIndex)
            {
                // File-based playback (robust parsing)
                string line = fileLines[currentLineIndex].Trim();
                currentLineIndex++;
                if (currentLineIndex >= fileLines.Length) currentLineIndex = dataStartIndex; // loop within data region

                if (string.IsNullOrWhiteSpace(line))
                {
                    await Task.Delay(10, cancellationToken);
                    continue;
                }

                // Skip obvious metadata/comment lines
                if (line.StartsWith("#") || line.IndexOf("SITE CODE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("SAMPLING RATE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("INITIAL TIME", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                // Try to extract numeric tokens robustly (first 3 numbers -> x,y,z ; optional 4th -> intensity)
                var tokens = line.Split(',');
                var nums = new List<double>(4);
                foreach (var t in tokens)
                {
                    if (nums.Count >= 4) break;
                    var s = t.Trim();
                    if (string.IsNullOrEmpty(s)) continue;
                    if (double.TryParse(s, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v))
                    {
                        nums.Add(v);
                    }
                    else
                    {
                        // some lines may contain localized minus or weird chars; try replacing common unicode minus
                        var replaced = s.Replace("−", "-");
                        if (double.TryParse(replaced, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out v))
                        {
                            nums.Add(v);
                        }
                    }
                }

                if (nums.Count >= 3)
                {
                    x = nums[0];
                    y = nums[1];
                    z = nums[2];
                    if (nums.Count >= 4) intensity = nums[3];
                }
                else
                {
                    // not a data line; skip
                    continue;
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

            string accLine = $"$XSACC,{x.ToString("F3", CultureInfo.InvariantCulture)},{y.ToString("F3", CultureInfo.InvariantCulture)},{z.ToString("F3", CultureInfo.InvariantCulture)}*00";
            string intLine = $"$XSINT,{intensity.ToString("F2", CultureInfo.InvariantCulture)}*00";

            DataReceived?.Invoke(this, accLine);
            DataReceived?.Invoke(this, intLine);

            // If the original data is recorded at high frequency (e.g. 100Hz), you may want a smaller delay.
            await Task.Delay(100, cancellationToken);
        }
    }

    public void Dispose()
    {
        DisconnectAsync().Wait();
    }
}
