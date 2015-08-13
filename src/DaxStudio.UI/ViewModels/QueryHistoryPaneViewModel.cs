using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.ViewModels
{
    public class QueryHistoryPaneViewModel : ToolWindowBase
    {
        private bool _isFilteredByServer;
        private bool _isFilteredByDatabase;

        [ImportingConstructor]
        public QueryHistoryPaneViewModel()
        { }

        

        public bool IsFilteredByServer
        {
            get { return _isFilteredByServer; }
            set
            {
                _isFilteredByServer = value;
                NotifyOfPropertyChange(() => IsFilteredByServer);
            }
        }
        public bool IsFilteredByDatabase
        { 
            get { return _isFilteredByDatabase; }
            set { 
                _isFilteredByDatabase = value;
                NotifyOfPropertyChange(() => IsFilteredByDatabase);
            }
        }
    }
}
