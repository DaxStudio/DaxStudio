using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{
    public class AllQueriesEventClassesDialogViewModel: Screen
    {
        public bool XmlaCommands { get; set; }
        public bool Queries { get; set; }
        public bool Errors { get; set; }
        public bool File { get; set; }
        public bool JobGraph { get; set; }
        public bool MDataProvider { get; set; }
        public bool ProgressReport { get; set; }
        public bool Security { get; set; }

        
    }
}
