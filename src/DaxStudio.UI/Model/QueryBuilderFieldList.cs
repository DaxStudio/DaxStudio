using ADOTabular;
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
        //IDropTarget, 
        IQueryBuilderFieldList
    {
        public QueryBuilderFieldList()
        {
            DropHandler = new QueryBuilderDropHandler(this);
        }

        public ObservableCollection<IADOTabularColumn> Items { get; } = new ObservableCollection<IADOTabularColumn>();
        public QueryBuilderDropHandler DropHandler { get; }

        #region IQueryBuilderFieldList
        public void Add(IADOTabularColumn item)
        {
            Items.Add(item);
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

        //public void DragOver(IDropInfo dropInfo)
        //{
        //    if (dropInfo.DragInfo == null)
        //    {
        //        dropInfo.Effects = DragDropEffects.None;
        //        return;
        //    }

        //    IADOTabularObject sourceItem = dropInfo.DragInfo.SourceItem as TreeViewColumn;
        //    var targetColl = dropInfo.TargetCollection as ObservableCollection<IADOTabularObject>;
        //    var sourceColl = dropInfo.DragInfo.SourceCollection as ObservableCollection<IADOTabularObject>;
        //    //QueryBuilderFieldList targetItem = dropInfo.TargetItem as QueryBuilderFieldList;

        //    if (sourceItem != null && targetColl != null &&  (!targetColl.Contains(sourceItem) || targetColl == sourceColl) ) 
        //    {

        //        //dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
        //        dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
        //        dropInfo.Effects = DragDropEffects.Move;
        //    }
        //    else
        //    {
        //        dropInfo.Effects = DragDropEffects.None;
        //    }

        //}

        //public void Drop(IDropInfo dropInfo)
        //{
        //    var obj = dropInfo.Data;
        //    var col = obj as TreeViewColumn;
        //    ObservableCollection<IADOTabularObject> targetColl = dropInfo.TargetCollection as ObservableCollection<IADOTabularObject>;
        //    int targetIdx = dropInfo.InsertIndex >= targetColl.Count && targetColl.Count > 0 ? targetColl.Count - 1 : dropInfo.InsertIndex;
        //    // check if we are moving within list
        //    if (dropInfo.TargetCollection == dropInfo.DragInfo.SourceCollection) {

        //        // move item in collection
                
        //        targetColl.Move(targetColl.IndexOf(col), targetIdx);
        //        return;
        //    }

        //    // don't add the same column twice
        //    if (Items.Contains(col)) return;

        //    // Inser new item
        //    if (dropInfo.InsertIndex == targetColl.Count) 
        //    {
        //        Items.Add(col);
        //    }
        //    else
        //    {
        //        Items.Insert(targetIdx, col);
        //    }
        //}

       
    }
}
