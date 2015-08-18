using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class FileSavedEvent: FileOpenedEvent
    {
        public FileSavedEvent(string fileName) : base(fileName) { }

    }
}
