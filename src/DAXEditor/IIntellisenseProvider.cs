using ICSharpCode.AvalonEdit.CodeCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DAXEditorControl
{
    public interface IIntellisenseProvider
    {
        void ProcessTextEntered(object sender, TextCompositionEventArgs e, ref CompletionWindow completionWindow);
        void ProcessTextEntering(object sender, TextCompositionEventArgs e, ref CompletionWindow completionWindow);
        void ProcessKeyDown(object sender, KeyEventArgs e);
    }
}
