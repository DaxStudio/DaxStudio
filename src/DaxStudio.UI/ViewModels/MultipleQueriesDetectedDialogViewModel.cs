using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;

namespace DaxStudio.UI.ViewModels
{
    [Export]
    public class MultipleQueriesDetectedDialogViewModel:Screen
    {
        public MultipleQueriesDetectedDialogViewModel(IGlobalOptions options)
        {
            Options = options;
        }

        public IGlobalOptions Options { get; }

        public int CharactersBeforeComment { get; set; }
        public int CharactersAfterComment { get; set; }

        public void KeepDirectQueryCode()
        {
            Result = MultipleQueriesDetectedDialogResult.KeepDirectQuery;
            if (RememberChoice)
            {
                // save Never Remove to options
                Options.EditorMultipleQueriesDetectedOnPaste = DaxStudio.Interfaces.Enums.MultipleQueriesDetectedOnPaste.AlwaysKeepBoth;
            }

            TryClose(true);
        }

        public void RemoveDirectQueryCode()
        {
            Result = MultipleQueriesDetectedDialogResult.RemoveDirectQuery;
            if (RememberChoice)
            {
                // save Always Remove to options
                Options.EditorMultipleQueriesDetectedOnPaste = DaxStudio.Interfaces.Enums.MultipleQueriesDetectedOnPaste.AlwaysKeepOnlyDax;
            }
            TryClose(true);
        }

        public void Cancel()
        {
            Result = MultipleQueriesDetectedDialogResult.Cancel;
        }

        public bool RememberChoice { get; set; }
        public MultipleQueriesDetectedDialogResult Result { get; private set; }
    }
}
