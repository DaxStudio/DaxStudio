using Microsoft.AnalysisServices.AdomdClient.Authentication;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DaxStudio.UI.Controls
{
    public class DataGridCustomCopy:DataGrid
    {

        protected override void OnExecutedCopy(ExecutedRoutedEventArgs args)
        {
            if (this.SelectedCells.Count == 1) { this.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader; }

            base.OnExecutedCopy(args);
            // get the content of the clipboard in csv format
            var str = Clipboard.GetText(TextDataFormat.CommaSeparatedValue);
            // and push only the csv format back onto the clipboard
            // this stops Excel using the default html format which ends up merging cells if there are embedded new lines.
            Clipboard.SetText(str, TextDataFormat.CommaSeparatedValue);
        }

    }
}
