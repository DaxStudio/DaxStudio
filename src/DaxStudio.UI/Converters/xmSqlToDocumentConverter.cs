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
using ICSharpCode.SharpDevelop.Dom;
using DaxStudio.Controls.PropertyGrid;

namespace DaxStudio.UI.Converters
{

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

        private IEnumerable<Inline> GetInlinesFromString(string input)
        {
            // Reduce CRLF and LF to CR only
            input = input.Replace("\r\n", "\r").Replace("\n", "\r");
            while (input.Contains('\r'))
            {
                int posCR = input.IndexOf('\r');
                string s = input.Substring(0, posCR);
                if (s.Length > 0) yield return new Run(s);
                yield return new LineBreak();
                input = input.Substring(posCR + 1);
            }
            if (input.Length > 0) yield return new Run(input);
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

                var formatList = new (string token, Action<Run> format, int position)[formats.Count];
                for (int i = 0; i < formats.Count; i++)
                {
                    formatList[i].token = formats[i].token;
                    formatList[i].format = formats[i].format;
                    formatList[i].position = 0;
                }

                // We stop the formatting after a timeout, we lose the format for long string,
                // but this makes the window more usable
                const long FORMAT_TIMEOUT_MILLISECONDS = 4000;
                DateTime formatTimeout = DateTime.Now.AddMilliseconds(FORMAT_TIMEOUT_MILLISECONDS);

                int sIndex = 0;
                while (sIndex < s.Length && DateTime.Now < formatTimeout)
                {
                    // Find token
                    for (int n = 0; n < formatList.Length; n++)
                    {
                        if (formatList[n].position >= 0)
                        {
                            // Skip search for positions that are not found in the previous iteration
                            formatList[n].position = s.IndexOf(formatList[n].token, sIndex, StringComparison.Ordinal);
                        }
                    }
                    int posEnd = s.IndexOf("|~E~|", sIndex, StringComparison.Ordinal);
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

                    //up to |~?~| is normal (where ? is the keyword)
                    paragraph.Inlines.AddRange(GetInlinesFromString(s.Substring(sIndex, posToken - sIndex)));

                    //between |~?~| and |~E~| is formatted
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
                    
                    // s = s.Substring(posEnd + 5);
                    sIndex = posEnd + 5;
                }
                if (sIndex < s.Length) {
                    s = s.Substring(sIndex);
                    foreach( var f in formats)
                    {
                        s = s.Replace(f.token, "");
                    }
                    s = s.Replace("|~E~|", "");
                    paragraph.Inlines.Add(new Run(s));
                    // paragraph.Inlines.AddRange(GetInlinesFromString(s.Substring(sIndex)));
                    // paragraph.Inlines.Add(new Run(s));
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
