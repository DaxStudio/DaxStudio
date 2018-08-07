using System;
using System.Windows.Media;
using System.Globalization;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Xml;

namespace DaxStudio.UI.Converters {
    /// <summary>
    /// Converts a string containing valid XAML into WPF objects.
    /// </summary>
    [ValueConversion(typeof(string), typeof(object))]
    public sealed class XmSqlToDocumentConverter : IValueConverter {
        /// <summary>
        /// Converts a string containing valid XAML into WPF objects.
        /// </summary>
        /// <param name="value">The string to convert.</param>
        /// <param name="targetType">This parameter is not used.</param>
        /// <param name="parameter">This parameter is not used.</param>
        /// <param name="culture">This parameter is not used.</param>
        /// <returns>A WPF object.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            FlowDocument doc = new FlowDocument();

           string s = value as string;
            if (s != null) {
                Paragraph paragraph = new Paragraph();
                // paragraph.Margin = new Thickness(0);
                while (s.IndexOf("|~S~|") != -1) {
                    //up to |~S~| is normal
                    paragraph.Inlines.Add(new Run(s.Substring(0, s.IndexOf("|~S~|"))));
                    //between |~S~| and |~E~| is highlighted
                    paragraph.Inlines.Add(new Run(s.Substring(s.IndexOf("|~S~|") + 5,
                                              s.IndexOf("|~E~|") - (s.IndexOf("|~S~|") + 5))) { FontWeight = FontWeights.Bold, Background = Brushes.Yellow });
                    //the rest of the string (after the |~E~|)
                    s = s.Substring(s.IndexOf("|~E~|") + 5);
                }
                if (s.Length > 0) {
                    paragraph.Inlines.Add(new Run(s));
                }

                doc.Blocks.Add(paragraph);

            }
            return doc;
        }

        /// <summary>
                /// Converts WPF framework objects into a XAML string.
                /// </summary>
                /// <param name="value">The WPF Famework object to convert.</param>
                /// <param name="targetType">This parameter is not used.</param>
                /// <param name="parameter">This parameter is not used.</param>
                /// <param name="culture">This parameter is not used.</param>
                /// <returns>A string containg XAML.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException("This converter cannot be used in two-way binding.");
        }
    }
}
