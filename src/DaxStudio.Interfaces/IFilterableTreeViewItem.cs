using System.Collections;
using System.Collections.Generic;

namespace DaxStudio.Interfaces
{
    public interface IFilterableTreeViewItem
    {
        string Name { get;  }
        void ApplyCriteria(string criteria, Stack<IFilterableTreeViewItem> nodes);
        bool IsMatch { get; set; }
        bool IsExpanded { get; set; }
        bool IsSelected { get; set; }
        IEnumerable<IFilterableTreeViewItem> Children { get; }
    }
}
