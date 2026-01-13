using YureteruWPF.Models;

namespace YureteruWPF.Services;

/// <summary>
/// Interface for parsing serial data
/// </summary>
public interface IDataParser
{
    /// <summary>
    /// Parse a line of serial data
    /// </summary>
    /// <param name="line">Raw data line</param>
    /// <returns>Parsed data or null if invalid</returns>
    object? ParseLine(string line);
}
