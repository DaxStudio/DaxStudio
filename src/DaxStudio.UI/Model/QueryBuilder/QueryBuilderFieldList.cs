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
                AddSortByColumn(col);
                AddGroupByColumns(col);
            }

            if (item.ObjectType == ADOTabularObjectType.Hierarchy || item.ObjectType == ADOTabularObjectType.UnnaturalHierarchy)
            {
                var hier = item as ADOTabularHierarchy;
                if (hier != null)
                {
                    // remove any columns currently in the list that are also in the hierarchy
                    foreach (var level in hier.Levels)
                    {
                        var existingCol = Items.FirstOrDefault(i => i.DaxName == level.DaxName);
                        if (existingCol != null)
                        {
                            Items.Remove(existingCol);
                        }
                    }
                }
            }
            Items.Add(builderItem);
            NotifyOfPropertyChange(nameof(Items));
        }

        //  automatically pull OrderBy columns into the query
        private void AddSortByColumn(ADOTabularColumn col)
        {
            if (col.OrderBy != null)
            {
                
                var sortCol = ((IADOTabularColumn)col.OrderBy) as QueryBuilderColumn;
                if (sortCol == null) sortCol = new QueryBuilderColumn(col.OrderBy, true, EventAggregator);
                sortCol.IsSortBy = true;
                if (!Items.Any(sort => sort.DaxName == sortCol.DaxName))
                {
                    Items.Add(sortCol);
                    EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, $"{col.OrderBy.DaxName} was added to the Query Builder because it is the OrderBy column for {col.DaxName}"));
                }
            }
        }

        // automatically pull GroupBy columns into the query
        private void AddGroupByColumns(ADOTabularColumn col)
        {
            if (col.GroupBy.Count > 0)
            {
                foreach (var grpCol in col.GroupBy)
                {
                    var groupCol = ((IADOTabularColumn)grpCol) as QueryBuilderColumn;
                    if (groupCol == null) groupCol = new QueryBuilderColumn(grpCol, true, EventAggregator);
                    groupCol.IsGroupBy = true;
                    if (!Items.Any(item => item.DaxName == groupCol.DaxName))
                    {
                        Items.Add(groupCol);
                        EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, $"{grpCol.DaxName} was added to the Query Builder because it is a GroupBy column for {col.DaxName}"));
                    }
                }
            }
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

        public void EditNewMeasure(QueryBuilderColumn measure)
        {
            EventAggregator.PublishOnUIThreadAsync(new ShowMeasureExpressionEditor(measure, true));
        }

        public void EditMeasure(QueryBuilderColumn measure)
        {
            EventAggregator.PublishOnUIThreadAsync(new ShowMeasureExpressionEditor(measure, false));
        }

        public void ChangeSortDirection(QueryBuilderColumn column)
        {
            switch (column.SortDirection)
            {
                case SortDirection.ASC:
                    column.SortDirection = SortDirection.DESC;
                    break;
                case SortDirection.DESC:
                    column.SortDirection = SortDirection.ASC;
                    break;
                //default:
                //    column.SortDirection = SortDirection.ASC;
                //    break;
            }
        }

        public void ToggleIsSorted(QueryBuilderColumn column)
        {
            switch (column.SortDirection)
            {
                case SortDirection.ASC:
                    column.SortDirection = SortDirection.None;
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
