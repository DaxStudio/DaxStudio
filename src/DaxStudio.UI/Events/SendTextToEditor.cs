using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.UI.Events
{
    public class SendTextToEditor
    {
        public SendTextToEditor(string textToSend) : this(textToSend, string.Empty, false)  { }
        public SendTextToEditor(string textToSend, bool runQuery) : this(textToSend, string.Empty, runQuery) { }
        public SendTextToEditor(string textToSend, string databaseName) : this(textToSend, databaseName, false) { }

        public SendTextToEditor(string textToSend, string databaseName, bool runQuery)
        {
            TextToSend = textToSend;
            DatabaseName = databaseName;
            RunQuery = runQuery;
        }

        public string TextToSend { get; }
        public string DatabaseName { get; }

       public bool RunQuery { get; }
    }
}
