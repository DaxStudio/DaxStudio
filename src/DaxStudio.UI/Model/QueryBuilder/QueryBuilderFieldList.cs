using ADOTabular;
using ADOTabular.Interfaces;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DaxStudio.UI.Extensions;

namespace DaxStudio.UI.Model
{
    public class QueryBuilderFieldList : 
        PropertyChangedBase,
        IQueryBuilderFieldList,
        IEnumerable<QueryBuilderColumn>
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

        public bool HasMeasures()
        {
            return this.Items.Any(c => c.IsMeasure());
        } 

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

        public void Add(QueryBuilderColumn item)
        {

            Items.Add(item);
            NotifyOfPropertyChange(nameof(Items));
        }

        public bool Contains(IADOTabularColumn item)
        {
            return Items.FirstOrDefault(c => c.TabularObject == item) != null;
            
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
            var item = Items.FirstOrDefault(c => c.TabularObject == obj);
            return Items.IndexOf(item);
        }

        public void EditMeasure(QueryBuilderColumn measure)
        {
            EventAggregator.PublishOnUIThread(new ShowMeasureExpressionEditor(measure));
        }


        #endregion

        #region IEnumerable Support
        public IEnumerator<QueryBuilderColumn> GetEnumerator() => Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();
        #endregion

        public void Clear()
        {
            Items.Clear();
        }
    }
}
