using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.ViewModels;

namespace DaxStudio.CommandLine.ViewModel
{
    /// <summary>
    /// CLI-compatible subclass of ServerTimesViewModel.
    /// Follows the CmdCustomTraceViewModel pattern — all timing logic
    /// (FE/SE gap analysis, ProcessResults) stays in the base class.
    /// </summary>
    internal class CmdServerTimesViewModel : ServerTimesViewModel
    {
        public CmdServerTimesViewModel(IEventAggregator eventAggregator,
            ServerTimingDetailsViewModel serverTimingDetails,
            IGlobalOptions options, IWindowManager windowManager)
            : base(eventAggregator, serverTimingDetails, options, windowManager)
        {
        }
    }
}
