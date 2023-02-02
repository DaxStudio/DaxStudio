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
using System.Collections.Generic;
using ICSharpCode.NRefactory.Parser;
using System.Linq;
using Fclp.Internals.Extensions;

namespace DaxStudio.UI.Converters {
    /// <summary>
    /// Converts a string containing valid XAML into WPF objects.
    /// </summary>
    [ValueConversion(typeof(string), typeof(object))]
    public sealed class XmSqlToDocumentConverter : IValueConverter {
        private void FormatHighlight(Run run)
        {
            run.FontWeight = FontWeights.Bold;
            run.SetResourceReference(Run.BackgroundProperty, "Theme.Brush.xmSQLHighlight.Back");
            run.SetResourceReference(Run.ForegroundProperty, "Theme.Brush.xmSQLHighlight.Fore");
        }
        private void FormatKeyword(Run run)
        {
            run.FontWeight = FontWeights.Bold;
        }
        private void FormatNumber(Run run) {
            run.FontWeight = FontWeights.Bold;
            run.SetResourceReference(Run.BackgroundProperty, "Theme.Brush.xmSQLNumber.Back");
            run.SetResourceReference(Run.ForegroundProperty, "Theme.Brush.xmSQLNumber.Fore");
        }
        private void FormatDaxCallback(Run run)
        {
            run.SetResourceReference(Run.BackgroundProperty, "Theme.Brush.xmSQLCallback.Back");
            run.SetResourceReference(Run.ForegroundProperty, "Theme.Brush.xmSQLCallback.Fore");
        }

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

                var formats = new List<(string token, Action<Run> format)> {
                    ( "|~S~|", FormatHighlight ),
                    ( "|~K~|", FormatKeyword ),
                    ( "|~N~|", FormatNumber ),
                    ( "|~F~|", FormatDaxCallback )
                };

                while (s.Length > 0)
                {
                    var formatList = new (string token, Action<Run> format, int position)[formats.Count];
                    for(int i = 0; i < formats.Count;i++)
                    {
                        formatList[i].token = formats[i].token;
                        formatList[i].format = formats[i].format;
                        formatList[i].position = 0;
                    }

                    // Find token
                    for (int n = 0; n < formatList.Length; n++)
                    {
                        formatList[n].position = s.IndexOf(formatList[n].token);
                    }
                    int posEnd = s.IndexOf("|~E~|");
                    var tokens = formatList.Where(t => t.position >= 0);
                    if (tokens.IsNullOrEmpty())
                    {
                        if (posEnd >= 0)
                        {
                            Debug.WriteLine($"IndexOf(|~E~|) = {posEnd} (initial token not found, see following dump of string to convert)");
                            Debug.WriteLine(s);
                        }
                        break;
                    }
                    int posToken = tokens.Min(t => t.position);
                    var tokenFirst = formatList.FirstOrDefault(t => t.position == posToken);

                    //up to |~S~| is normal
                    paragraph.Inlines.Add(new Run(s.Substring(0, posToken)));
                    //between |~S~| and |~E~| is highlighted
                    int length = posEnd - (posToken + 5);
                    if (length < 0)
                    {
                        Debug.WriteLine($"IndexOf(|~E~|) - IndexOf({tokenFirst.token}) = {length} (should not be negative, see following dump of string to convert)");
                        Debug.WriteLine(s);
                        break;
                    }
                    var run = new Run(s.Substring(posToken + 5, length));
                    tokenFirst.format(run);
                    paragraph.Inlines.Add(run);

                    s = s.Substring(posEnd + 5);
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
