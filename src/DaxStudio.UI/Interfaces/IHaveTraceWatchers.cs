using Caliburn.Micro;

namespace DaxStudio.UI.Interfaces
{
    interface IHaveTraceWatchers
    {
        BindableCollection<ITraceWatcher> TraceWatchers { get; }
    }
}
