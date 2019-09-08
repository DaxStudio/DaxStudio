using System;
using System.Globalization;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    public class MaxMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if ((Double)values[0] > (Double)values[1]) return values[0];
            return values[1];
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
