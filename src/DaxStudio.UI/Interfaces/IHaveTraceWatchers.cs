using Caliburn.Micro;
using DaxStudio.Core.Interfaces;

namespace DaxStudio.UI.Interfaces
{
    public interface IHaveTraceWatchers
    {
        BindableCollection<ITraceWatcher> TraceWatchers { get; }
    }
}
