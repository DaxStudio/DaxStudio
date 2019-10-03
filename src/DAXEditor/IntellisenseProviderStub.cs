using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAXEditorControl
{
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
    }
}
