using System;

namespace YureteruWPF.Models;

public enum DataType
{
    Acceleration,
    Intensity,
    Raw
}

public class ParsedData
{
    public DataType Type { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public object? Value { get; set; }
}
