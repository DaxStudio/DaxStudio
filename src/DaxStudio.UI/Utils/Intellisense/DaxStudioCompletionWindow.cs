using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Editing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils.Intellisense
{
    public class DaxStudioCompletionWindow : CompletionWindow
    {
        public DaxStudioCompletionWindow(TextArea textArea) : base(textArea) { }

        public Action<CompletionWindow> DetachCompletionEvents { get; set; }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            DetachCompletionEvents?.Invoke(this);
        }
    }
}
