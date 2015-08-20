using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    class MaxTextLinesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string str = value as string;
            string param = parameter as string;
            int? maxLines = int.Parse(param.Split(',')[0]);
            int? height = int.Parse(param.Split(',')[1]);
            if (str != null)
            {
                if (str.Split('\n').Length > maxLines) return height;
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
