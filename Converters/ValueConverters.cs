using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YureteruWPF.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}

public class BoolToStatusBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b)
        {
            return Application.Current?.Resources["StatusOnlineBrush"] as System.Windows.Media.Brush
                   ?? System.Windows.Media.Brushes.Green;
        }
        return Application.Current?.Resources["StatusOfflineBrush"] as System.Windows.Media.Brush
               ?? System.Windows.Media.Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StringVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IntensityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double intensity) return System.Windows.Media.Brushes.White;

        if (intensity < 0.5) return Application.Current.Resources["JmaCol0"];
        if (intensity < 1.5) return Application.Current.Resources["JmaCol1"];
        if (intensity < 2.5) return Application.Current.Resources["JmaCol2"];
        if (intensity < 3.5) return Application.Current.Resources["JmaCol3"];
        if (intensity < 4.5) return Application.Current.Resources["JmaCol4"];
        if (intensity < 5.0) return Application.Current.Resources["JmaCol5L"];
        if (intensity < 5.5) return Application.Current.Resources["JmaCol5U"];
        if (intensity < 6.0) return Application.Current.Resources["JmaCol6L"];
        if (intensity < 6.5) return Application.Current.Resources["JmaCol6U"];
        return Application.Current.Resources["JmaCol7"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class LpgmClassToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int classValue) return System.Windows.Media.Brushes.Transparent;

        return classValue switch
        {
            1 => Application.Current.Resources["JmaLpgmCol1"],
            2 => Application.Current.Resources["JmaLpgmCol2"],
            3 => Application.Current.Resources["JmaLpgmCol3"],
            4 => Application.Current.Resources["JmaLpgmCol4"],
            _ => System.Windows.Media.Brushes.Transparent
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
