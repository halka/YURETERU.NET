using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using YureteruWPF.Models;
using YureteruWPF.Utilities;

namespace YureteruWPF.Services;

/// <summary>
/// Event recording service (ported from useEventRecording.js)
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

    public EventRecordingService(double threshold = 0.5)
    {
        _threshold = threshold;
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
                var seismicEvent = new SeismicEvent
                {
                    Timestamp = DateTime.Now,
                    MaxIntensity = _maxIntensity,
                    MaxGal = _maxGal,
                    MaxLpgmClass = _maxLpgmClass,
                    MaxSva = _maxSva
                };

                // Add to beginning of collection
                EventHistory.Insert(0, seismicEvent);

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

    public void ExportToCsv(string filePath)
    {
        if (EventHistory.Count == 0) return;

        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,MaxIntensity,MaxGal,MaxLPGMClass,MaxSva");

        foreach (var evt in EventHistory)
        {
            csv.AppendLine($"{evt.FormattedTimestamp},{evt.MaxIntensity:F3},{evt.MaxGal:F2},{evt.MaxLpgmClass},{evt.MaxSva:F2}");
        }

        File.WriteAllText(filePath, csv.ToString());
    }
}
