using ADOTabular;
using Caliburn.Micro;
using DaxStudio.UI.Events;
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
        public QueryBuilderFieldList(IEventAggregator eventAggregator)
        {
            EventAggregator = eventAggregator;
            DropHandler = new QueryBuilderDropHandler(this);
        }

        public void Remove(QueryBuilderColumn item)
        {
            Items.Remove(item);
            NotifyOfPropertyChange(nameof(Items));
        }
        public ObservableCollection<QueryBuilderColumn> Items { get; } = new ObservableCollection<QueryBuilderColumn>();
        public IEventAggregator EventAggregator { get; }
        public QueryBuilderDropHandler DropHandler { get; }

        #region IQueryBuilderFieldList
        public void Add(IADOTabularColumn item)
        {
            var builderItem = item as QueryBuilderColumn;
            if (builderItem == null)
                 builderItem = new QueryBuilderColumn(item, true);
            if (item is ADOTabularColumn col)
            {
                builderItem.SelectedTable = col.Table;
            }
            Items.Add(builderItem);
            NotifyOfPropertyChange(nameof(Items));
        }

        public bool Contains(IADOTabularColumn item)
        {
            return Items.FirstOrDefault(c => c == item) != null;
            
        }

        public int Count => Items.Count;

        public void Move(int oldIndex, int newIndex)
        {
            Items.Move(oldIndex, newIndex);
        }
        public void Insert(int index, IADOTabularColumn item)
        {
            var builderItem = new QueryBuilderColumn(item,true);
            // if we are 'inserting' at the end just do an add
            if (index >= Items.Count) Items.Add(builderItem);
            else Items.Insert(index, builderItem);
        }

        public int IndexOf(IADOTabularColumn obj)
        {
            var item = Items.FirstOrDefault(c => c == obj);
            return Items.IndexOf(item);
        }

        public void EditMeasure(QueryBuilderColumn measure)
        {
            EventAggregator.PublishOnUIThread(new ShowMeasureExpressionEditor(measure));
        }
        #endregion


    }
}
