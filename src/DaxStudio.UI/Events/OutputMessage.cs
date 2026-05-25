using DaxStudio.Core.Events;
using DaxStudio.UI.ViewModels;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace DaxStudio.UI.Events
{

    public class LocationOutputMessage : OutputMessage  {
        public LocationOutputMessage(MessageType messageType, string text, int row, int column): base(messageType, text) {

            Row = row;
            Column = column;
        }
        public FlowDocument Document
        {
            get
            {
                var doc = new FlowDocument
                {
                    PagePadding = new Thickness(0),
                    ColumnWidth = double.PositiveInfinity
                };
                Paragraph para = new Paragraph
                {
                    Margin = new Thickness(0)
                };
                doc.Blocks.Clear();
                doc.Blocks.Add(para);
                // if we have a row and column then add a hyperlink to the location
                if (Row != -1 && Column != -1)
                {
                    var run2 = new Run($"Goto ({Row},{Column})");

                    Hyperlink hlink = new Hyperlink(run2)
                    {
                        ToolTip = $"Go to location ({Row},{Column})",
                        Cursor = Cursors.Hand
                    };
                    var parent = Parent as OutputPaneViewModel;
                    var self = this;
                    hlink.Click += (s, e) => parent?.GotoLocation.Execute(self);
                    para.Inlines.Add(hlink);
                    para.Inlines.Add(" ");
                }

                Run run = new Run(Text);
                para.Inlines.Add(run);

                return doc;
            }
        }

        public int Row { get; protected set; } = -1;
        public int Column { get; protected set; } = -1;
    }
}
