using Caliburn.Micro;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.Views
{
    public class RefreshProgressViewModel 
        : ToolWindowBase
        , IHaveLongRunningOperation
    {
        public RefreshProgressViewModel(IEventAggregator eventAggregator)
        {
            EventAggregator = eventAggregator;
        }

        public override string Title => "Refresh Progress";

        public override string DefaultDockingPane => "Bottom";

        public override string ContentId => "RefreshProgress";
    
        public bool IsRunning => throw new System.NotImplementedException();

        public IEventAggregator EventAggregator { get; }

        public void Cancel()
        {
            throw new System.NotImplementedException();
        }
    }
}
