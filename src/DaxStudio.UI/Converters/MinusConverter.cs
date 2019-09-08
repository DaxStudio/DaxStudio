using System;
using System.Globalization;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    class MinusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double dblParam = 0.0;
            double.TryParse((string)parameter, out dblParam);
            return (double)value - (double)dblParam;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
