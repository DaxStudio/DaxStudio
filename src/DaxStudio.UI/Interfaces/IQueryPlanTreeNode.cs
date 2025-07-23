using Caliburn.Micro;
using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.Interfaces
{
    public interface IQueryPlanTreeNode<T> where T : QueryPlanRow
    {
        IObservableCollection<T> Children { get; }
    }
}