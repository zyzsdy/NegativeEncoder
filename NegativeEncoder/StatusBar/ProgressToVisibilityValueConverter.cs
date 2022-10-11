using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NegativeEncoder.StatusBar;

public class ProgressToVisibilityValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value != null)
        {
            var v = (int)value;
            if (v == 0) return "Hidden";
            if (v < 0) return "Collapsed";
            return "Visible";
        }

        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return 0;
    }
}