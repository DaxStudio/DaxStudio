using System;
using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.UI.Controls
{
    public class DataGridNoContextMenu:DataGrid
    {
        protected override void OnContextMenuOpening(ContextMenuEventArgs e)
        {
            // HACK: this control swallows the OnContextMenuOpening event which seems to cause issues
            //       in the base class when the current row is not selected. 
            //base.OnContextMenuOpening(e);
            e.Handled = true;
        }

    }
}
