using System;
using YureteruWPF.Models;

namespace YureteruWPF.Services;

/// <summary>
/// Parses serial data from seismometer (ported from parser.js)
/// Supports formats: $XSACC, $XSINT, $XSRAW
/// </summary>
public class DataParser : IDataParser
{
    public object? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith('$'))
            return null;

        try
        {
            // Split by checksum
            var parts = line.Trim().Split('*');
            if (parts.Length < 1) return null;

            var content = parts[0];
            var segments = content.Split(',');
            var type = segments[0];

            if (type == "$XSACC")
            {
                // $XSACC,x,y,z
                if (segments.Length < 4) return null;

                return new AccelerationData
                {
                    Timestamp = DateTime.Now,
                    X = double.Parse(segments[1]),
                    Y = double.Parse(segments[2]),
                    Z = double.Parse(segments[3])
                };
            }
            else if (type == "$XSINT")
            {
                // $XSINT,...,value
                var value = double.Parse(segments[^1]);
                return new ParsedData { Type = DataType.Intensity, Value = value };
            }
            else if (type == "$XSRAW")
            {
                // $XSRAW,...
                return new ParsedData { Type = DataType.Raw, Value = segments[1..] };
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Parse error: {ex.Message}, Line: {line}");
            return null;
        }
    }
}
