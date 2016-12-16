using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;


namespace DaxStudio.UI.Converters
{
    class OffsetConverter : MarkupExtension, IValueConverter
    {

        private static OffsetConverter _instance;

        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Convert.ToDouble(value) - System.Convert.ToDouble(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }

        #endregion

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return _instance ?? (_instance = new OffsetConverter());
        }
 
    }
}
