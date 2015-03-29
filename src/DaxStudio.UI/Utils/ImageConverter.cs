using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DaxStudio.UI.Utils
{
    [ValueConversion(typeof(object), typeof(ImageSource))]
        public class ImageResourceConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {

                if (value == null) return null;

                return Application.Current.TryFindResource(value); // Use the application as root.

            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) 
            { throw new System.NotImplementedException(); }

        }
    
}
