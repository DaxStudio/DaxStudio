using DaxStudio.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    public class OutputTargetImageResourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            OutputTarget target = (OutputTarget) value;
            switch(target)
            {
                case OutputTarget.Clipboard: return "results_clipboardDrawingImage";
                case OutputTarget.File: return "results_fileDrawingImage";
                case OutputTarget.Grid: return "results_tableDrawingImage";
                case OutputTarget.Linked: return "results_excel_linkedDrawingImage";
                case OutputTarget.Static: return "results_excelDrawingImage";
                case OutputTarget.Timer: return "results_timerDrawingImage";
                default: return null;
            }

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
