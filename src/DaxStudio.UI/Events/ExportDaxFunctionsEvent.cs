using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class ExportDaxFunctionsEvent
    {
        public readonly bool _autoDelete;
        public ExportDaxFunctionsEvent(bool autoDelete)
        {
            _autoDelete = autoDelete;
        }

        public ExportDaxFunctionsEvent() {  }

        public bool AutoDelete {
            get { return _autoDelete; }
        }
    }
}
