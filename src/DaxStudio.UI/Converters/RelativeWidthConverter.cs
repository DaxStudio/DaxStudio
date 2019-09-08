using System;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    public class RelativeWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values[0] == System.Windows.DependencyProperty.UnsetValue) return 0.0;
            return ((Double)values[0] * (Double)values[1]) ;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
