using System;
using System.Windows.Media;
using System.Globalization;
using System.Diagnostics;
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

            string tabReplacement = new string(' ', 4);
            string s = (value as string).Replace("\t", tabReplacement);
            if (s != null) {
                Paragraph paragraph = new Paragraph();
                // paragraph.Margin = new Thickness(0);
                int posHighlight = s.IndexOf("|~S~|");
                int posKeyword = s.IndexOf("|~K~|");
                int posEnd = s.IndexOf("|~E~|");
                while (posHighlight != -1 || posKeyword != -1) {
                    if (posHighlight >= 0 && (posHighlight < posKeyword || posKeyword == -1))
                    {
                        //up to |~S~| is normal
                        paragraph.Inlines.Add(new Run(s.Substring(0, posHighlight)));
                        //between |~S~| and |~E~| is highlighted
                        int length = posEnd - (posHighlight + 5);
                        if (length < 0)
                        {
                            Debug.WriteLine($"IndexOf(|~E~|) - IndexOf(|~E~|) = {length} (should not be negative, see following dump of string to convert)");
                            Debug.WriteLine(s);
                            break;
                        }
                        var highlightRun = new Run(s.Substring(posHighlight + 5, length))
                        { FontWeight = FontWeights.Bold };
                        highlightRun.SetResourceReference(Run.BackgroundProperty, "Theme.Brush.xmSQLHighlight.Back");
                        highlightRun.SetResourceReference(Run.ForegroundProperty, "Theme.Brush.xmSQLHighlight.Fore");
                        paragraph.Inlines.Add(highlightRun);
                        //the rest of the string (after the |~E~|)
                    }
                    else if (posKeyword >= 0)
                    {
                        //up to |~K~| is normal
                        paragraph.Inlines.Add(new Run(s.Substring(0, posKeyword)));
                        //between |~K~| and |~E~| is highlighted
                        int length = posEnd - (posKeyword + 5);
                        if (length < 0)
                        {
                            Debug.WriteLine($"IndexOf(|~E~|) - IndexOf(|~E~|) = {length} (should not be negative, see following dump of string to convert)");
                            Debug.WriteLine(s);
                            break;
                        }
                        var highlightRun = new Run(s.Substring(posKeyword + 5, length))
                        { FontWeight = FontWeights.Bold };
                        //highlightRun.SetResourceReference(Run.BackgroundProperty, "Theme.Brush.xmSQLHighlight.Back");
                        //highlightRun.SetResourceReference(Run.ForegroundProperty, "Theme.Brush.xmSQLHighlight.Fore");
                        paragraph.Inlines.Add(highlightRun);
                        //the rest of the string (after the |~E~|)
                    }
                    s = s.Substring(posEnd + 5);

                    posHighlight = s.IndexOf("|~S~|");
                    posKeyword = s.IndexOf("|~K~|");
                    posEnd = s.IndexOf("|~E~|");
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
