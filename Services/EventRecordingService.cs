using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using YureteruWPF.Models;

namespace YureteruWPF.Services;

/// <summary>
/// Event recording service (persisted)
/// </summary>
public class EventRecordingService : IEventRecordingService
{
    private readonly double _threshold;
    private bool _isRecording;
    private double _maxIntensity;
    private double _maxGal;
    private int _maxLpgmClass;
    private double _maxSva;

    public bool IsRecordingEvent { get; private set; }
    public ObservableCollection<SeismicEvent> EventHistory { get; } = new();

    // lock object for EventHistory snapshot/export/persist
    private readonly object _historyLock = new();

    // Keep history bounded to avoid unbounded memory growth
    private const int MaxHistory = 200;

    private readonly string _historyFilePath;

    public EventRecordingService(double threshold = 0.5)
    {
        _threshold = threshold;

        // Determine history file path in LocalApplicationData
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "YureteruWPF");
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch
        {
            dir = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
        }
        _historyFilePath = Path.Combine(dir, "history.json");

        // Load persisted history (if any)
        TryLoadHistory();
    }

    private void TryLoadHistory()
    {
        try
        {
            if (!File.Exists(_historyFilePath)) return;

            var json = File.ReadAllText(_historyFilePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) return;

            var loaded = JsonSerializer.Deserialize<SeismicEvent[]>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (loaded == null || loaded.Length == 0) return;

            // Populate ObservableCollection on construction (caller is likely on UI thread)
            lock (_historyLock)
            {
                EventHistory.Clear();
                foreach (var evt in loaded.Take(MaxHistory))
                {
                    EventHistory.Add(evt);
                }
            }
        }
        catch
        {
            // ignore load errors (corrupt file etc.)
        }
    }

    private Task SaveHistoryAsync()
    {
        // Snapshot under lock
        SeismicEvent[] snapshot;
        lock (_historyLock)
        {
            snapshot = EventHistory.ToArray();
        }

        if (snapshot == null) return Task.CompletedTask;

        return Task.Run(async () =>
        {
            var temp = Path.GetTempFileName();
            try
            {
                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(temp, json, Encoding.UTF8).ConfigureAwait(false);

                // Replace destination
                File.Copy(temp, _historyFilePath, true);
            }
            catch
            {
                // ignore save errors
            }
            finally
            {
                try { File.Delete(temp); } catch { }
            }
        });
    }

    public void ProcessIntensity(double intensity, double peakGal, int lpgmClass, double sva)
    {
        if (!_isRecording)
        {
            // Check if we should start recording an event
            if (intensity >= _threshold)
            {
                _isRecording = true;
                _maxIntensity = intensity;
                _maxGal = peakGal;
                _maxLpgmClass = lpgmClass;
                _maxSva = sva;
                IsRecordingEvent = true;
            }
        }
        else
        {
            // Update maximum value during recording
            if (intensity > _maxIntensity) _maxIntensity = intensity;
            if (peakGal > _maxGal) _maxGal = peakGal;
            if (lpgmClass > _maxLpgmClass) _maxLpgmClass = lpgmClass;
            if (sva > _maxSva) _maxSva = sva;

            // Check if event has ended
            if (intensity < _threshold)
            {
                var seismicEvent = new SeismicEvent(DateTime.Now, _maxIntensity, _maxGal, _maxLpgmClass, _maxSva);

                // Insert at beginning and enforce bounded history.
                lock (_historyLock)
                {
                    EventHistory.Insert(0, seismicEvent);

                    // Trim oldest entries if exceeding MaxHistory
                    while (EventHistory.Count > MaxHistory)
                    {
                        EventHistory.RemoveAt(EventHistory.Count - 1);
                    }
                }

                // Persist snapshot asynchronously (fire-and-forget)
                _ = SaveHistoryAsync();

                // Reset recording state
                _isRecording = false;
                _maxIntensity = 0;
                _maxGal = 0;
                _maxLpgmClass = 0;
                _maxSva = 0;
                IsRecordingEvent = false;
            }
        }
    }

    /// <summary>
    /// Export history to CSV without blocking the UI. The actual file write is performed on a background thread.
    /// </summary>
    public void ExportToCsv(string filePath)
    {
        // Take a snapshot under lock to ensure consistency
        SeismicEvent[] snapshot;
        lock (_historyLock)
        {
            snapshot = EventHistory.ToArray();
        }

        if (snapshot == null || snapshot.Length == 0) return;

        // Run actual IO on threadpool to avoid blocking caller/UI
        _ = Task.Run(async () =>
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var sb = new StringBuilder(64 * (snapshot.Length + 1));
                sb.AppendLine("Timestamp,MaxIntensity,MaxGal,MaxLPGMClass,MaxSva");
                foreach (var evt in snapshot)
                {
                    sb.AppendLine($"{evt.FormattedTimestamp},{evt.MaxIntensity:F3},{evt.MaxGal:F2},{evt.MaxLpgmClass},{evt.MaxSva:F2}");
                }

                // Use asynchronous write to temp file
                await File.WriteAllTextAsync(tempFile, sb.ToString(), Encoding.UTF8).ConfigureAwait(false);

                // Overwrite destination (atomicity varies by platform)
                File.Copy(tempFile, filePath, true);
            }
            catch
            {
                // swallow exceptions here; caller may show status via callback if needed
            }
            finally
            {
                try { File.Delete(tempFile); } catch { /* ignore cleanup errors */ }
            }
        });
    }
}
