using Caliburn.Micro;
using DaxStudio.UI.ViewModels;
using System.Collections.Generic;

namespace DaxStudio.UI.Interfaces
{
    public interface IQueryPlanTreeNode<T> where T : QueryPlanRow
    {
        List<T> Children { get; }
    }
}