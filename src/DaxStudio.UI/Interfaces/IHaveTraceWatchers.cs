using Caliburn.Micro;

namespace DaxStudio.UI.Interfaces
{
    public interface IHaveTraceWatchers
    {
        BindableCollection<ITraceWatcher> TraceWatchers { get; }
    }
}
