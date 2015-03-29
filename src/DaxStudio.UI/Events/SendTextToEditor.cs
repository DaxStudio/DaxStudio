using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.UI.Events
{
    public class SendTextToEditor
    {
        public SendTextToEditor(string textToSend)
        {
            TextToSend = textToSend;
        }

        public string TextToSend { get; set; }
    }
}
