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
        }

        public string Text { get; set; }
    }
    
}
