using Caliburn.Micro;
using DaxStudio.UI.ViewModels;
using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace DaxStudio.UI.Events
{

    public class LocationOutputMessage :OutputMessage  { 
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
                    var parent = Parent;
                    var self = this;
                    hlink.Click += (s, e) => parent.GotoLocation.Execute(self);
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

    public class FolderOutputMessage : OutputMessage {
        public FolderOutputMessage(string text, string folder):base(MessageType.Information, text)
        {
            FolderPath = folder;
        }
        public string FolderPath { get; private set; }

        public FlowDocument Document
        {
            get
            {
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
                Run run = new Run(Text);
                para.Inlines.Add(run);

                // if we have a row and column then add a hyperlink to the location
                if (!string.IsNullOrEmpty(FolderPath))
                {
                    var run2 = new Run($"Open Folder");

                    Hyperlink hlink = new Hyperlink(run2)
                    {
                        ToolTip = $"Open {FolderPath}",
                        Cursor = Cursors.Hand
                    };
                    var parent = Parent;
                    var self = this;
                    hlink.Click += (s, e) => parent.OpenFolder.Execute(self);
                    para.Inlines.Add(new LineBreak());
                    para.Inlines.Add(hlink);
                    
                }

                return doc;
            }
        }

    }


    public class OutputMessage : PropertyChangedBase
    {
        private readonly double _durationMs = double.NaN;
        internal OutputMessage() { }
        // constructor for syntax errors

        public OutputMessage(MessageType messageType, string text, double durationMs) : this (messageType,text)
        {
            _durationMs = durationMs;
        }


        public OutputMessage(MessageType messageType, string text)
        {
            Text = text;
            MessageType = messageType;
            Start = DateTime.Now;
            _durationMs = double.NaN;
        }
        public OutputPaneViewModel Parent { get; set; }
        public bool ActivateOutput { get; set; }
        
        public string Text { get; set; }
        public DateTime Start { get; set; }
        public MessageType MessageType { get; set; }


        public double DurationMs { get { return _durationMs; } }
        public string DurationString {
            get
            {
                if (double.IsNaN(_durationMs ))
                    return string.Empty;
                return _durationMs.ToString("#,##0");
            }
        }

        public string DurationTooltip { 
            get {
                if (double.IsNaN( DurationMs )) return string.Empty;
                return $"{DurationString} ms  ({TimeSpan.FromMilliseconds(DurationMs):h\\:mm\\:ss\\.fff})"; 
            } 
        }

    }

    public enum MessageType
    {
        Information
        ,Warning
        ,Error
        ,Success
    }
     
}
