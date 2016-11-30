using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    class CappedRowHeightConverter :IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var maxHeight = Double.Parse(parameter.ToString());
            var actHeight1 = (Double)values[0];
            var actHeight2 = (Double)values[1];
            if (actHeight1 > maxHeight )
            { return maxHeight; }
            else if (actHeight2 > actHeight1)
            { return actHeight2; }
            else
            { return actHeight1; }

        }

        object[] IMultiValueConverter.ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            return new object[] { Binding.DoNothing };
        }
    }
}
