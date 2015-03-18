using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class SetSelectedWorksheetEvent
    {
        public SetSelectedWorksheetEvent(string worksheet)
        {
            _worksheet = worksheet;
        }
        private readonly string _worksheet;
        public string Worksheet { get { return _worksheet; } }
    }
}
