using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.UI.Events
{
    public class OutputInformationMessageEvent
    {
        public OutputInformationMessageEvent(string text)
        {
            Text = text;
            IsDurationSet = false;
        }

        public OutputInformationMessageEvent(string text, Double duration)
        {
            Text = text;
            Duration = duration;
            IsDurationSet = true;
        }

        public string Text { get; set; }
        public Double Duration { get; set; }
        public bool IsDurationSet { get; private set; }
    }
    
}
