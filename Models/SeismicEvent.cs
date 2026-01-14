using System;

namespace YureteruWPF.Models;

/// <summary>
/// Represents a recorded seismic event (immutable)
/// </summary>
public record SeismicEvent(DateTime Timestamp, double MaxIntensity, double MaxGal, int MaxLpgmClass, double MaxSva)
{
    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
}
