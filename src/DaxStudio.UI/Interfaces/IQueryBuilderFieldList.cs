using ADOTabular.Interfaces;
using System.Collections.Specialized;

namespace DaxStudio.UI.Interfaces
{
    public interface IQueryBuilderFieldList : INotifyCollectionChanged
    {
        bool Contains(IADOTabularColumn item);
        void Insert(int index, IADOTabularColumn item);
        void Add(IADOTabularColumn item);

        int Count { get; }
        void Move(int oldIndex, int newIndex);
        int IndexOf(IADOTabularColumn obj);
    }
}
