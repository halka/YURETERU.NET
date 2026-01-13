using System;
using System.Collections.ObjectModel;
using YureteruWPF.Models;

namespace YureteruWPF.Services;

/// <summary>
/// Interface for event recording service
/// </summary>
public interface IEventRecordingService
{
    bool IsRecordingEvent { get; }
    ObservableCollection<SeismicEvent> EventHistory { get; }
    
    void ProcessIntensity(double intensity, double peakGal, int lpgmClass, double sva);
    void ExportToCsv(string filePath);
}
