using Caliburn.Micro;
using DaxStudio.Core.Trace;
using DaxStudio.Interfaces;

namespace DaxStudio.CommandLine.ViewModel
{
    /// <summary>
    /// CLI-compatible subclass of ServerTimesModel.
    /// Provides no-op overrides for UI-only operations that are abstract on the Core base.
    /// </summary>
    internal class CmdServerTimesViewModel : ServerTimesModel
    {
        public CmdServerTimesViewModel(IEventAggregator eventAggregator,
            ServerTimingDetailsViewModel serverTimingDetails,
            IGlobalOptions options, IWindowManager windowManager)
            : base(eventAggregator, serverTimingDetails, options, windowManager)
        {
        }

        public override void CopyResults() { }
    }
}
