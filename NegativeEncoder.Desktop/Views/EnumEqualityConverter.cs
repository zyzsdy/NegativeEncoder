using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using NegativeEncoder.Desktop.Models;

namespace NegativeEncoder.Desktop.Views;

public sealed class EnumEqualityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value?.ToString() == parameter?.ToString();
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && Enum.TryParse<EncodeMode>(parameter?.ToString(), out var mode) ? mode : AvaloniaProperty.UnsetValue;
}
