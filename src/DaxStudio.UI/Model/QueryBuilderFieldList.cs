using ADOTabular;
using Caliburn.Micro;
using DaxStudio.UI.Interfaces;
using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DaxStudio.UI.Model
{
    public class QueryBuilderFieldList : 
        PropertyChangedBase,
        IQueryBuilderFieldList
    {
        public QueryBuilderFieldList()
        {
            DropHandler = new QueryBuilderDropHandler(this);
        }

        public void Remove(IADOTabularColumn item)
        {
            Items.Remove(item);
            NotifyOfPropertyChange(nameof(Items));
        }
        public ObservableCollection<IADOTabularColumn> Items { get; } = new ObservableCollection<IADOTabularColumn>();
        public QueryBuilderDropHandler DropHandler { get; }

        #region IQueryBuilderFieldList
        public void Add(IADOTabularColumn item)
        {
            Items.Add(item);
            NotifyOfPropertyChange(nameof(Items));
        }

        public bool Contains(IADOTabularColumn item)
        {
            return Items.Contains(item);
        }

        public int Count => Items.Count;

        public void Move(int oldIndex, int newIndex)
        {
            Items.Move(oldIndex, newIndex);
        }
        public void Insert(int index, IADOTabularColumn item)
        {
            // if we are 'inserting' at the end just do an add
            if (index >= Items.Count) Items.Add(item);
            else Items.Insert(index, item);
        }

        public int IndexOf(IADOTabularColumn obj)
        {
            return Items.IndexOf(obj);
        }
        #endregion

       
    }
}
