using ADOTabular.Interfaces;

namespace DaxStudio.UI.Interfaces
{
    public interface IQueryBuilderFieldList
    {
        bool Contains(IADOTabularColumn item);
        void Insert(int index, IADOTabularColumn item);
        void Add(IADOTabularColumn item);

        int Count { get; }
        void Move(int oldIndex, int newIndex);
        int IndexOf(IADOTabularColumn obj);
    }
}
