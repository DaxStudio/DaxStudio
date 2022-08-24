using System;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    class IntToDoubleConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int)
            {
                return System.Convert.ToDouble(value);
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double) return System.Convert.ToInt32(value);
            return Binding.DoNothing;
        }
    }
}
