using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DaxStudio.UI.Events
{
    public class SendTextToEditor
    {
        public SendTextToEditor(string textToSend) : this(textToSend, string.Empty, false, false)  { }
        public SendTextToEditor(string textToSend, bool runQuery) : this(textToSend, string.Empty, runQuery, false) { }
        public SendTextToEditor(string textToSend, bool runQuery, bool replaceQueryBuilderQuery) : this(textToSend, string.Empty, runQuery, replaceQueryBuilderQuery) { }
        public SendTextToEditor(string textToSend, string databaseName) : this(textToSend, databaseName, false, false) { }

        public SendTextToEditor(string textToSend, string databaseName, bool runQuery, bool replaceQueryBuilderQuery)
        {
            TextToSend = textToSend;
            DatabaseName = databaseName;
            RunQuery = runQuery;
            ReplaceQueryBuilderQuery = replaceQueryBuilderQuery;
        }

        public string TextToSend { get; }
        public string DatabaseName { get; }

       public bool RunQuery { get; }
        public bool ReplaceQueryBuilderQuery { get; }
    }
}
