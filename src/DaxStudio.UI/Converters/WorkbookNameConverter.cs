using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    public class WorkbookNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var name = value as string;
            if (name != null)
            {
                try
                {
                    var f = System.IO.Path.GetFileName((string)name);
                    return f;
                }
                catch
                {
                    return (string)name;
                }
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
