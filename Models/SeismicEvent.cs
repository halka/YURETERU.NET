using System;

namespace YureteruWPF.Models;

/// <summary>
/// Represents a recorded seismic event
/// </summary>
public class SeismicEvent
{
    public DateTime Timestamp { get; set; }
    public double MaxIntensity { get; set; }
    public double MaxGal { get; set; }
    public int MaxLpgmClass { get; set; }
    public double MaxSva { get; set; }
    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
}
