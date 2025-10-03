using Caliburn.Micro;
using DaxStudio.UI.ViewModels;
using Microsoft.PowerBI.Api.Models;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

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
                var doc = new FlowDocument();
                doc.PagePadding = new Thickness(0);
                doc.ColumnWidth = double.PositiveInfinity;
                Paragraph para = new Paragraph();
                para.Margin = new Thickness(0);
                doc.Blocks.Clear();
                doc.Blocks.Add(para);
                // if we have a row and column then add a hyperlink to the location
                if (Row != -1 && Column != -1)
                {
                    //var img = new Image();
                    //img.SetResourceReference(Image.SourceProperty, "linkDrawingImage");
                    //img.Width = 15;
                    //img.Height = 15;
                    //img.Margin = new Thickness(0, 4, 5, 0);
                    ////img.Source = new DynamicResourceExtension("linkDrawingImage").ProvideValue(null) as ImageSource;
                    //var container = new InlineUIContainer();
                    //container.Child = img;
                    //para.Inlines.Add(container);

                    var run2 = new Run($"Goto ({Row},{Column})");
                    
                    Hyperlink hlink = new Hyperlink(run2);
                    hlink.ToolTip = $"Go to location ({Row},{Column})";
                    hlink.Command = Parent.GotoLocation;
                    hlink.CommandParameter = this;
                    hlink.Cursor = Cursors.Hand;
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
                var doc = new FlowDocument();
                doc.PagePadding = new Thickness(0,2,0,2);
                doc.ColumnWidth = double.PositiveInfinity;
                Paragraph para = new Paragraph();
                para.Margin = new Thickness(0);
                doc.Blocks.Clear();
                doc.Blocks.Add(para);
                Run run = new Run(Text);
                para.Inlines.Add(run);

                // if we have a row and column then add a hyperlink to the location
                if (!string.IsNullOrEmpty(FolderPath))
                {
                    //var img = new Image();
                    //img.SetResourceReference(Image.SourceProperty, "linkDrawingImage");
                    //img.Width = 15;
                    //img.Height = 15;
                    //img.Margin = new Thickness(0, 4, 5, 0);
                    //var container = new InlineUIContainer();
                    //container.Child = img;
                    //para.Inlines.Add(container);

                    var run2 = new Run($"Open Folder");

                    Hyperlink hlink = new Hyperlink(run2);
                    hlink.ToolTip = $"Open {FolderPath}";
                    hlink.Command = Parent.OpenFolder;
                    hlink.CommandParameter = this;
                    hlink.Cursor = Cursors.Hand;
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
