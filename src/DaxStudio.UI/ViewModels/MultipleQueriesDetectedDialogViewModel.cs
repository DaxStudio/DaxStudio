using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;

namespace DaxStudio.UI.ViewModels
{
    [Export]
    public class MultipleQueriesDetectedDialogViewModel:BaseDialogViewModel
    {
        public MultipleQueriesDetectedDialogViewModel(IGlobalOptions options)
        {
            Options = options;
        }

        public IGlobalOptions Options { get; }

        public int CharactersBeforeComment { get; set; }
        public int CharactersAfterComment { get; set; }

        public string Comment { get; set; } = "// SQL Query";
        public async void KeepDirectQueryCode()
        {
            Result = MultipleQueriesDetectedDialogResult.KeepDirectQuery;
            if (RememberChoice)
            {
                // save Never Remove to options
                Options.EditorMultipleQueriesDetectedOnPaste = DaxStudio.Interfaces.Enums.MultipleQueriesDetectedOnPaste.AlwaysKeepBoth;
            }

            await TryCloseAsync(true);
        }

        public async void RemoveDirectQueryCode()
        {
            Result = MultipleQueriesDetectedDialogResult.RemoveDirectQuery;
            if (RememberChoice)
            {
                // save Always Remove to options
                Options.EditorMultipleQueriesDetectedOnPaste = DaxStudio.Interfaces.Enums.MultipleQueriesDetectedOnPaste.AlwaysKeepOnlyDax;
            }
            await TryCloseAsync(true);
        }

        public override void Close()
        {
            Result = MultipleQueriesDetectedDialogResult.Cancel;
            TryCloseAsync();
        }

        public bool RememberChoice { get; set; }
        public MultipleQueriesDetectedDialogResult Result { get; private set; }
    }
}
