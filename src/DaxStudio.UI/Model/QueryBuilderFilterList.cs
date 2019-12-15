using ADOTabular;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    public class QueryBuilderFilterList :  IQueryBuilderFieldList
    {
        public QueryBuilderFilterList()
        {
            DropHandler = new QueryBuilderDropHandler(this);
        }


        public ObservableCollection<TreeViewColumnFilter> Items { get; } = new ObservableCollection<TreeViewColumnFilter>();
        public QueryBuilderDropHandler DropHandler { get; }

        public IEnumerable<FilterType> FilterTypes
        {
            get
            {
                var items = Enum.GetValues(typeof(FilterType)).Cast<FilterType>();
                return items;
            }
        }



        #region IQueryBuilderFieldList
        public void Add(IADOTabularColumn item)
        {
            var filter = new TreeViewColumnFilter(item);
            Items.Add(filter);
        }

        public bool Contains(IADOTabularColumn item)
        {
            return Items.FirstOrDefault(f => f.TabularObject == item) != null;
        }
        public int Count => Items.Count;

        public int IndexOf(IADOTabularColumn obj)
        {
            var item = Items.FirstOrDefault(f => f.TabularObject == obj);
            return Items.IndexOf(item);
        }
        public void Insert(int index, IADOTabularColumn item)
        {
            var filter = new TreeViewColumnFilter(item);
            // if we are 'inserting' at the end just do an add
            if (index >= Items.Count) Items.Add(filter);
            Items.Insert(index, filter);
        }
        public void Move(int oldIndex, int newIndex)
        {
            Items.Move(oldIndex, newIndex);
        }
        #endregion
    }
}
