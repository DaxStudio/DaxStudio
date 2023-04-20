using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.UI.Interfaces;
using Serilog;
using System;
using System.ComponentModel.Composition;

namespace DaxStudio.UI.Utils {
    public class ApplicationCloseCheck : IResult {
        
        //readonly Action<IDialogManager, Action<bool>> closeCheck;
        readonly Func<bool> closeCheck;
        readonly IChild screen;

        public ApplicationCloseCheck(IChild screen, Func<bool> closeCheck) {
            this.screen = screen;
            this.closeCheck = closeCheck;
        }

        [Import]
        public IShell Shell { get; set; }

        public void Execute(CoroutineExecutionContext context) {
            try
            {
                var documentWorkspace = screen.Parent as IDocumentWorkspace;
                if (documentWorkspace != null)
                {
                    documentWorkspace.Activate(screen);
                }
            }
            catch (Exception ex) {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(ApplicationCloseCheck), nameof(Execute), $"Error in close check\n{ex.Message}");
            }
            finally
            {
                Completed(this, new ResultCompletionEventArgs { WasCancelled = !closeCheck() });
            }
        }

        public event EventHandler<ResultCompletionEventArgs> Completed = delegate { };
    }
}