using DaxStudio.Core.Events;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace DaxStudio.UI.Converters
{
    public class FolderOutputMessageToDocumentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is FolderOutputMessage msg)) return null;

            var doc = new FlowDocument
            {
                PagePadding = new Thickness(0, 2, 0, 2),
                ColumnWidth = double.PositiveInfinity
            };
            Paragraph para = new Paragraph
            {
                Margin = new Thickness(0)
            };
            doc.Blocks.Clear();
            doc.Blocks.Add(para);
            Run run = new Run(msg.Text);
            para.Inlines.Add(run);

            if (!string.IsNullOrEmpty(msg.FolderPath))
            {
                var run2 = new Run("Open Folder");
                var folderPath = msg.FolderPath;

                Hyperlink hlink = new Hyperlink(run2)
                {
                    ToolTip = $"Open {folderPath}",
                    Cursor = Cursors.Hand
                };
                hlink.Click += (s, e) =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folderPath,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                };
                para.Inlines.Add(new LineBreak());
                para.Inlines.Add(hlink);
            }

            return doc;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
