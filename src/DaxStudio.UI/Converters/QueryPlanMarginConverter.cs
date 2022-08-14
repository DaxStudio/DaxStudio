using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    internal class QueryPlanMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is int level)) return Binding.DoNothing;
            var spacing = 0;
            var offset = 0;
            var parameters = ((string)parameter).Trim().Split(' ');
            if (!(int.TryParse(parameters[0].ToString(), out spacing))) return Binding.DoNothing;
            if(parameters.Length ==2) int.TryParse(parameters[1].ToString(), out offset);
            return new Thickness(offset + (spacing * level), 0, 0, 0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
