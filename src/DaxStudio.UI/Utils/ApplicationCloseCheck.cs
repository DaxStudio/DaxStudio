using Caliburn.Micro;
using DaxStudio.UI.Interfaces;
using System;
using System.ComponentModel.Composition;

namespace DaxStudio.UI.Utils {
    public class ApplicationCloseCheck : IResult {
        
        //readonly Action<IDialogManager, Action<bool>> closeCheck;
        readonly Action<Action<bool>> closeCheck;
        readonly IChild screen;

        public ApplicationCloseCheck(IChild screen,  Action<Action<bool>> closeCheck) {
            this.screen = screen;
            this.closeCheck = closeCheck;
        }

        [Import]
        public IShell Shell { get; set; }

        public void Execute(CoroutineExecutionContext context) {
            var documentWorkspace = screen.Parent as IDocumentWorkspace;
            if (documentWorkspace != null)
                documentWorkspace.Activate(screen);

            closeCheck(result => Completed(this, new ResultCompletionEventArgs { WasCancelled = !result }));
        }

        public event EventHandler<ResultCompletionEventArgs> Completed = delegate { };
    }
}