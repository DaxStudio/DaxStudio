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
        private bool _xmlaCommands;
        private bool _queries = true;
        private bool _errors = true;
        private bool _file;
        private bool _jobGraph;
        private bool _mDataProvider;
        private bool _progressReport;
        private bool _security;

        public bool XmlaCommands { get => _xmlaCommands; 
            set { 
                _xmlaCommands = value; 
                NotifyOfPropertyChange(); 
            } 
        }
        public bool Queries { get => _queries; set { _queries = value; NotifyOfPropertyChange(); } }
        public bool Errors { get => _errors; set { _errors = value; NotifyOfPropertyChange(); }  }
        public bool File { get => _file; set { _file = value; NotifyOfPropertyChange(); } }
        public bool JobGraph { get => _jobGraph; set { _jobGraph = value; NotifyOfPropertyChange(); } }
        public bool MDataProvider { get => _mDataProvider; set { _mDataProvider = value;NotifyOfPropertyChange(); } }
        public bool ProgressReport { get => _progressReport; set { _progressReport = value; NotifyOfPropertyChange(); } }
        public bool Security { get => _security; set { _security = value;NotifyOfPropertyChange(); } }

        public bool IsCancelled { get; set; }

        public void Cancel()
        {
            IsCancelled = true;
        }

        public void Ok()
        {
            TryClose(true);
        }
    }
}
