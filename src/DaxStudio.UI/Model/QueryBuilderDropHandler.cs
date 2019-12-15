using ADOTabular;
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
            
            var dragItem = dropInfo.DragInfo.SourceItem as TreeViewColumn;
            IADOTabularColumn sourceItem = dragItem.Column as IADOTabularColumn;
            //var targetColl = dropInfo.TargetCollection as IList;
            var sourceColl = dropInfo.DragInfo.SourceCollection as ObservableCollection<IADOTabularColumn>;
            //QueryBuilderFieldList targetItem = dropInfo.TargetItem as QueryBuilderFieldList;

            if (sourceItem != null && List != null && (!List.Contains(sourceItem) || List == sourceColl))
            {

                //dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
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
            var obj = dropInfo.Data as TreeViewColumn;

            IADOTabularColumn col = obj.Column as IADOTabularColumn;

            // check if we are moving within list
            if (dropInfo.TargetCollection == dropInfo.DragInfo.SourceCollection)
            {

                // move item in collection
                var targetIdx = dropInfo.InsertIndex > List.Count && List.Count > 0 ? List.Count - 1 : dropInfo.InsertIndex;
                List.Move(List.IndexOf(col), targetIdx);
                return;
            }

            // don't add the same column twice
            if (List.Contains(col)) return;

            // Inser new item
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
