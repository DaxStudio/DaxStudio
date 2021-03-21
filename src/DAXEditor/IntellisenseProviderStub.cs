using ICSharpCode.AvalonEdit;

namespace DAXEditorControl
{
    // This Intellisense Stub class is used when the editor is not connected to any 
    // data source (or if Intellisense is switched off in the options)
    // so none of the functions do anything by design.
    class IntellisenseProviderStub:IIntellisenseProvider
    {
        public void ProcessTextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e, ref ICSharpCode.AvalonEdit.CodeCompletion.CompletionWindow completionWindow)
        {
            // Do Nothing
        }

        public void ProcessTextEntering(object sender, System.Windows.Input.TextCompositionEventArgs e, ref ICSharpCode.AvalonEdit.CodeCompletion.CompletionWindow completionWindow)
        {
            // Do Nothing
        }

        public void ProcessKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Do Nothing
        }

        public string GetCurrentWord(TextViewPosition pos)
        {
            return string.Empty;
        }

        public void ShowInsight(string funcName)
        {
            // Do Nothing
        }

        public void ShowInsight(string funcName, int offset)
        {
            // Do Nothing
        }
    }
}
