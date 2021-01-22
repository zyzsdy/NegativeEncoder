using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace NegativeEncoder.EncodingTask
{
    public class TaskNameConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2) throw new Exception("TaskNameConverter 必须绑定2个对象");

            if (values[0] != null && values[1] != null)
            {
                if ((bool)(values[0]))
                {
                    return $"已完成 ({values[1]})";
                }
                else
                {
                    return $"正在编码 （{values[1]})";
                }
            }

            return "编码任务";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return targetTypes.Select(p => DependencyProperty.UnsetValue).ToArray();
        }
    }
}
