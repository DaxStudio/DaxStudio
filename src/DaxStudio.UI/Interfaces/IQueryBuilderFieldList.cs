using ADOTabular;
using ADOTabular.Interfaces;
using DaxStudio.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
