using System;
using System.Globalization;
using System.Windows.Data;

namespace DaxStudio.UI.Converters.CircularProgressBar
{
    internal class ArcStrokeThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // using Log to calculate a Stroke Thickness based on the width of the 
            // circular progress bar
            var width = (double)value;
            return Math.Log( width);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
