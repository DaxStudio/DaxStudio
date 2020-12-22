using System;

namespace DaxStudio.UI.Events
{
    public class OutputMessage
    {
        private readonly double _durationMs = double.NaN;

        // constructor for syntax errors
        public OutputMessage(MessageType messageType, string text, int row, int column) : this(messageType,text)
        {
            Row = row;
            Column = column;
            LocationSet = true;
        }
        public OutputMessage(MessageType messageType, string text, double durationMs) : this (messageType,text)
        {
            _durationMs = durationMs;
        }

        public OutputMessage(MessageType messageType, string text)
        {
            Row = -1;
            Column = -1;
            Text = text;
            MessageType = messageType;
            Start = DateTime.Now;
            _durationMs = double.NaN;
        }

        public string Text { get; set; }
        public DateTime Start { get; set; }
        public MessageType MessageType { get; set; }
        public int Row { get; private set; }
        public int Column { get; private set; }
        public bool LocationSet { get; private set; }
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

        public void MessageDoubleClick()
        {
            System.Diagnostics.Debug.WriteLine("message double click");
        }
    }

    public enum MessageType
    {
        Information
        ,Warning
        ,Error
    }
     
}
