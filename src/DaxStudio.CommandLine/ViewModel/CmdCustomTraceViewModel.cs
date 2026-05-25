using Caliburn.Micro;
using DaxStudio.Core.Trace;
using DaxStudio.Interfaces;

namespace DaxStudio.CommandLine.ViewModel
{
    internal class CmdCustomTraceViewModel : CustomTraceModel
    {
        public CmdCustomTraceViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager) : base(eventAggregator, globalOptions, windowManager)
        {
        }

        public override bool ShouldStartTrace()
        {
            return true;
        }

        public override void CopyAll() { }
    }
}
