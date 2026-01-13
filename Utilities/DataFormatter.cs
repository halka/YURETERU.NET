using System;
using System.Windows.Media;

namespace YureteruWPF.Utilities;

/// <summary>
/// Data formatting utilities (ported from formatting.js)
/// </summary>
public static class DataFormatter
{
    /// <summary>
    /// Get color based on JMA (Japan Meteorological Agency) seismic intensity scale
    /// </summary>
    public static Color GetJMAColor(double value)
    {
        if (value < 0.5) return Color.FromRgb(0x88, 0x88, 0x88); // 0
        if (value < 1.5) return Color.FromRgb(0x66, 0xcc, 0xff); // 1
        if (value < 2.5) return Color.FromRgb(0x00, 0xff, 0x99); // 2
        if (value < 3.5) return Color.FromRgb(0xff, 0xff, 0x00); // 3
        if (value < 4.5) return Color.FromRgb(0xff, 0xcc, 0x00); // 4
        if (value < 5.0) return Color.FromRgb(0xff, 0x99, 0x00); // 5-
        if (value < 5.5) return Color.FromRgb(0xff, 0x44, 0x00); // 5+
        if (value < 6.0) return Color.FromRgb(0xff, 0x00, 0x00); // 6-
        if (value < 6.5) return Color.FromRgb(0xaa, 0x00, 0x00); // 6+
        return Color.FromRgb(0xcc, 0x00, 0xff); // 7
    }

    /// <summary>
    /// Get color based on Gal (acceleration) value
    /// </summary>
    public static Color GetGalColor(double value)
    {
        if (value < 5.0) return Colors.Gray;
        if (value < 20.0) return Color.FromRgb(0x66, 0xcc, 0xff);
        if (value < 50.0) return Color.FromRgb(0x00, 0xff, 0x99);
        if (value < 100.0) return Color.FromRgb(0xff, 0xff, 0x00);
        if (value < 200.0) return Color.FromRgb(0xff, 0x99, 0x00);
        if (value < 400.0) return Color.FromRgb(0xff, 0x00, 0x00);
        return Color.FromRgb(0xaa, 0x00, 0x00);
    }

    /// <summary>
    /// Format date time to standard string
    /// </summary>
    public static string FormatDateTime(DateTime date)
    {
        return date.ToString("yyyy-MM-dd HH:mm:ss.fff");
    }
}
