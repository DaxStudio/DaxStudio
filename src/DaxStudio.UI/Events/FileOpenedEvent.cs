using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class FileOpenedEvent
    {
        public FileOpenedEvent(string fileName)
        {
            FileName = fileName;
        }

        public string FileName { get; private set; }
    }
}
