using ADOTabular;
using ADOTabular.Interfaces;
using DaxStudio.UI.Interfaces;
using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DaxStudio.UI.Model
{
    public class QueryBuilderDropHandler : IDropTarget
    {
        public IQueryBuilderFieldList List { get; }
        public QueryBuilderDropHandler(IQueryBuilderFieldList list)
        {
            List = list;
        }
    
    //    public ObservableCollection<IADOTabularObject> Items { get; } = new ObservableCollection<IADOTabularObject>();
        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.DragInfo == null)
            {
                dropInfo.Effects = DragDropEffects.None;
                return;
            }
            IADOTabularColumn adoCol;
            // if we are re-ordering columns sourceItem will be non-null
            adoCol = dropInfo.DragInfo.SourceItem as IADOTabularColumn;
            var treeViewCol = dropInfo.DragInfo.SourceItem as TreeViewColumn;
            var queryBuilderColumn = dropInfo.DragInfo.SourceItem as QueryBuilderColumn;
            
            // if we are dragging a column from the metadata pane it will be detected here
            if (adoCol == null)
            {
                if (treeViewCol == null && queryBuilderColumn == null)
                {
                    dropInfo.Effects = DragDropEffects.None;
                    return;
                }
                
                adoCol = treeViewCol?.Column as IADOTabularColumn;
                if (adoCol == null) adoCol = queryBuilderColumn?.TabularObject;

            }

            if (adoCol == null)
            {
                // this is an unknown source item so exit here
                dropInfo.Effects = DragDropEffects.None;
                return;
            }
            
            var sourceColl = dropInfo.DragInfo.SourceCollection as ObservableCollection<IADOTabularColumn>;
            
            if (adoCol != null && List != null                        // if we have a valid item and List
                && ((!List.Contains(adoCol) && treeViewCol != null))|| treeViewCol == null)   // and if we are dragging from the metadata pane and this item is not already in the list


            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
            else
            {
                dropInfo.Effects = DragDropEffects.None;
            }

        }

        public void Drop(IDropInfo dropInfo)
        {
            //var objTreeViewCol = dropInfo.DragInfo.DataObject as TreeViewColumn;
            //var queryBuilderCol = dropInfo.DragInfo.DataObject as QueryBuilderColumn;
            IADOTabularColumn col; // = dropInfo.DragInfo.DataObject as IADOTabularColumn;

            var dragObject = dropInfo.DragInfo.DataObject ?? dropInfo.DragInfo.Data;
            
            switch (dragObject )
            {
                case TreeViewColumn objTreeViewColumn:
                    col = objTreeViewColumn.Column as IADOTabularColumn;
                    break;
                case QueryBuilderColumn queryBuilderColumn:
                    col = queryBuilderColumn.TabularObject;
                    break;
                case IADOTabularColumn adoCol:
                    col = adoCol;
                    break;
                default:
                    return;
            }
            
            //if (col == null & objTreeViewCol != null )
            //{
            //    col = objTreeViewCol.Column as IADOTabularColumn;
            //}
            //if (col == null && objTreeViewCol == null)
            //{
            //    col = dropInfo.DragInfo.Data as IADOTabularColumn;
            //}

            // check if we are moving within list
            if (dropInfo.TargetCollection == dropInfo.DragInfo.SourceCollection)
            {

                // move item in collection
                var targetIdx = dropInfo.InsertIndex >= List.Count && List.Count > 0 ? List.Count - 1 : dropInfo.InsertIndex;
                List.Move(List.IndexOf(col), targetIdx);
                return;
            }

            // don't add the same column twice
            if (List.Contains(col)) return;

            // Insert new item
            if (dropInfo.InsertIndex == List.Count)
            {
                List.Add(col);
            }
            else
            {
                List.Insert(dropInfo.InsertIndex, col);
            }
        }
    }

}
