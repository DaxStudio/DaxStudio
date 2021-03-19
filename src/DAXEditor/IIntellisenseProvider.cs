using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using System.Windows.Input;

namespace DAXEditorControl
{
    public interface IIntellisenseProvider
    {
        void ProcessTextEntered(object sender, TextCompositionEventArgs e, ref CompletionWindow completionWindow);
        void ProcessTextEntering(object sender, TextCompositionEventArgs e, ref CompletionWindow completionWindow);
        void ProcessKeyDown(object sender, KeyEventArgs e);
        string GetCurrentWord(TextViewPosition pos);
        void ShowInsight(string funcName);
        void ShowInsight(string funcName, int offset);
    }
}
