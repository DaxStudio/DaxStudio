using ADOTabular;
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
    public class QueryBuilderFieldList : IDropTarget
    {
        public ObservableCollection<IADOTabularObject> Items { get; } = new ObservableCollection<IADOTabularObject>();
        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.DragInfo == null)
            {
                dropInfo.Effects = DragDropEffects.None;
                return;
            }

            IADOTabularObject sourceItem = dropInfo.DragInfo.SourceItem as TreeViewColumn;
            //QueryBuilderFieldList targetItem = dropInfo.TargetItem as QueryBuilderFieldList;

            if (sourceItem != null) // && targetItem != null ) //&& targetItem.CanAcceptChildren)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Copy;
            }
            else
            {
                dropInfo.Effects = DragDropEffects.None;
            }

        }

        public void Drop(IDropInfo dropInfo)
        {
            var obj = dropInfo.Data;
            var col = obj as TreeViewColumn;
            // todo - check if object is already in collection before adding it again
            Items.Add(col);
        }
    }
}
