using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace DaxStudio.UI.Converters
{
    class CappedHeightConverter :IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {

            var maxHeight = Double.Parse(parameter.ToString());
            var actHeight = (Double)value;
            if (actHeight > maxHeight)
            { return maxHeight; }
            else
            { return actHeight; }

        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
