using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.ViewModels;

namespace DaxStudio.CommandLine.ViewModel
{
    internal class CmdCustomTraceViewModel : CustomTraceViewModel
    {
        public CmdCustomTraceViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager) : base(eventAggregator, globalOptions, windowManager)
        {
        }

        public override bool ShouldStartTrace()
        {
            return true;
        }
    }
}
