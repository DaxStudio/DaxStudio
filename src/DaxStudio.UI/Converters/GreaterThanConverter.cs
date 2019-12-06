using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Markup;

namespace DaxStudio.UI.Converters
{
    public class GreaterThanConverter : MarkupExtension, IValueConverter
    {
        //  The only public constructor is one that requires a double argument.
        //  Because of that, the XAML editor will put a blue squiggly on it if 
        //  the argument is missing in the XAML. 
        public GreaterThanConverter(int opnd)
        {
            Operand = opnd;
        }

        /// <summary>
        /// Converter returns true if value is greater than this.
        /// 
        /// Don't let this be public, because it's required to be initialized 
        /// via the constructor. 
        /// </summary>
        protected int Operand { get; set; }

        //  When the XAML is parsed, each markup extension is instantiated 
        //  and the parser asks it to provide its value. Here, the value is 
        //  us. 
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int.TryParse(value.ToString(), out int i);
            return i > Operand;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
