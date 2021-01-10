using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace NegativeEncoder.Presets.Converters
{
    public class TitleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 3) throw new Exception("TitleConverter 必须绑定3个对象");

            var title = "消极压制";

            if (values[0] != null)
            {
                title += $" v{values[0]}";
            }

            if (values[1] != null && values[2] != null)
            {
                if ((bool)(values[2]))
                {
                    title += $" (预设: *{values[1]})";
                }
                else
                {
                    title += $" (预设: {values[1]})";
                }
            }

            return title;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return targetTypes.Select(p => DependencyProperty.UnsetValue).ToArray();
        }
    }
}
