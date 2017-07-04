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
            DatabaseName = string.Empty;
        }
        public SendTextToEditor(string textToSend, string databaseName)
        {
            TextToSend = textToSend;
            DatabaseName = databaseName;
        }

        public string TextToSend { get; set; }
        public string DatabaseName { get; set; }
    }
}
