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
            int? maxlines = int.Parse((string)parameter);
            
            //int? height = int.Parse(param.Split(',')[1]);
            if (str == null) return Binding.DoNothing;
            if (maxlines == null) return Binding.DoNothing;

            string[] lines = str.Split('\n');
            if (lines.Length <= maxlines) return str;
            return string.Join("\n", lines.Take((int)maxlines).ToArray<string>()) + "...";
            
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
