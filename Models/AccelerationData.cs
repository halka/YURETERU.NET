using System;

namespace YureteruWPF.Models;

/// <summary>
/// Represents acceleration data from the seismometer
/// </summary>
public class AccelerationData
{
    public DateTime Timestamp { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double VectorMagnitude { get; set; }
    public double Gal { get; set; }
}
