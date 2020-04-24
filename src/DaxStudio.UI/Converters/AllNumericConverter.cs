using DaxStudio.UI.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    public class AllNumericConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string[] strings = Array.ConvertAll(values, x => x.ToString());
            if (strings == null) return Binding.DoNothing;
            return strings.All<string>(v => v.IsNumeric() && !string.IsNullOrEmpty(v));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
