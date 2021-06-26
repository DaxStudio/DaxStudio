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
                 builderItem = new QueryBuilderColumn(item, true, EventAggregator);
            if (item is ADOTabularColumn col)
            {
                builderItem.SelectedTable = col.Table;
                if (col.OrderBy != null)
                {
                    // TODO - look at automatically pulling OrderBy columns into the query
                    var sortCol = ((IADOTabularColumn)col.OrderBy) as QueryBuilderColumn;
                    if (sortCol == null) sortCol = new QueryBuilderColumn(col.OrderBy, true, EventAggregator);
                    sortCol.IsSortBy = true;
                    if (!Items.Any(sort => sort.DaxName == sortCol.DaxName)) { 
                        Items.Add(sortCol);
                        EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Information, $"{col.OrderBy.DaxName} was added to the Query Builder because it is the OrderBy column for {col.DaxName}"));
                    }
                }
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
            NotifyOfPropertyChange(nameof(Items));
        }
        public void Insert(int index, IADOTabularColumn item)
        {
            var builderItem = new QueryBuilderColumn(item,true, EventAggregator);
            // if we are 'inserting' at the end just do an add
            if (index >= Items.Count) Items.Add(builderItem);
            else Items.Insert(index, builderItem);
            NotifyOfPropertyChange(nameof(Items));
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

        public void ChangeSortDirection(QueryBuilderColumn column)
        {
            switch (column.SortDirection)
            {
                case SortDirection.ASC:
                    column.SortDirection = SortDirection.DESC;
                    break;
                case SortDirection.DESC:
                    column.SortDirection = SortDirection.None;
                    break;
                default:
                    column.SortDirection = SortDirection.ASC;
                    break;
            }
        }


        //public bool CanDuplicateMeasure
        //{
        //    get => !string.IsNullOrEmpty(Selected.MeasureExpression);
        //}
        //public void DuplicateMeasure(QueryBuilderColumn measure)
        public void DuplicateMeasure(object measure)
        {
            System.Diagnostics.Debug.WriteLine("Duplicating Measure");
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
