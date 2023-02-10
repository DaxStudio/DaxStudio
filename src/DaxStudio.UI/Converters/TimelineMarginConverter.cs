using DaxStudio.Common;
using Serilog;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DaxStudio.UI.Converters
{
    internal class TimelineMarginConverter : IMultiValueConverter
    {
        const double RightMargin = 2;
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null) return Binding.DoNothing;
            Debug.Assert(values.Length == 3 || values.Length == 4, $"The {nameof(TimelineMarginConverter)} needs 3-4 parameters");
            if (values.Length != 3 && values.Length != 4) { return Binding.DoNothing; }
            
            try
            {
                double.TryParse(values[0]?.ToString(), out var cellWidth);
                long.TryParse(values[1]?.ToString(), out var offset);
                long.TryParse(values[2]?.ToString(), out var totalWidth);

                cellWidth -= RightMargin;
                // restrict offset and totalWidth to positive values
                if (offset < 0) offset = 0;
                if (totalWidth <= 1) totalWidth = 1;
                var verticalMargin = 0.0;
                if (values.Length == 4) double.TryParse(values[3]?.ToString(), out verticalMargin);
                var newOffset = (cellWidth / totalWidth) * offset;

                return new Thickness(newOffset, verticalMargin, 0, verticalMargin);
            }
            catch (Exception ex)
            {
                Debug.Assert(false, "Error calculating Timeline margin");
                Log.Error(ex, Constants.LogMessageTemplate, nameof(TimelineMarginConverter), nameof(Convert), "Error calculating timeline margin");
                return Binding.DoNothing;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
