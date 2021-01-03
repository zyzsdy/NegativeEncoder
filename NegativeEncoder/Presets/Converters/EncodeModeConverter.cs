using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace NegativeEncoder.Presets.Converters
{
    public class EncodeModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                var v = (EncodeMode)value;
                return v == (EncodeMode)int.Parse(parameter.ToString());
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var isChecked = (bool)value;
            if (isChecked)
            {
                return (EncodeMode)int.Parse(parameter.ToString());
            }

            return DependencyProperty.UnsetValue;
        }
    }
}
