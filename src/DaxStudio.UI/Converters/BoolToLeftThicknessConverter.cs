using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    internal class BoolToLeftThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool )
            {
                if ((bool)value)
                {
                    var left = System.Convert.ToDouble(parameter, CultureInfo.InvariantCulture);
                    return new Thickness(left, 0, 0, 0);
                }
                return new Thickness(0.0);
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
