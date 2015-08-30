using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    public class MaxLinesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string text = value as string;
            int maxLines;
            int.TryParse(parameter.ToString(), out maxLines);
            if (text != null && maxLines > 0)
            {
                var lines = text.Split('\n');
                if (lines.Length > maxLines)
                {
                    return string.Join("\n", lines.Take((int)maxLines).ToArray<string>()) + "…";
                }
                return text;
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
