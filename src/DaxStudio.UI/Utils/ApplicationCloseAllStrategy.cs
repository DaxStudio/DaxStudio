using System;
using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.Utils {
    public class ApplicationCloseAllStrategy : ICloseStrategy<DocumentTabViewModel> {
        //IEnumerator<IScreen> enumerator;
        private IEnumerable<DocumentTabViewModel> toclose; 
        bool finalResult;
        Action<bool, IEnumerable<DocumentTabViewModel>> callback;

        public void Execute(IEnumerable<DocumentTabViewModel> toClose, Action<bool, IEnumerable<DocumentTabViewModel>> callback)
        {
            //enumerator = toClose.GetEnumerator();
            toclose = toClose;
            this.callback = callback;
            finalResult = true;
            
            Evaluate(finalResult);
        }

        void Evaluate(bool result)
        {
            finalResult = finalResult && result;

            //if (!enumerator.MoveNext() || !result) // if last object or something was cancelled
            //    callback(finalResult, new List<IScreen>());
            //else
            //{
                
                if (toclose.Any())   
                {
                   var tasks =  toclose //conductor.GetChildren()
                        .OfType<IHaveShutdownTask>()
                        .Select(x => x.GetShutdownTask())
                        .Where(x => x != null);

                    // TODO - show dialog box here 

                    var sequential = new SequentialResult(tasks.GetEnumerator());
                    sequential.Completed += (s, e) =>
                        {
                            callback(!e.WasCancelled, new List<DocumentTabViewModel>());
                        //if(!e.WasCancelled)
                        //Evaluate(!e.WasCancelled);
                    };
                    sequential.Execute(new CoroutineExecutionContext());
                }
                else callback(true, new List<DocumentTabViewModel>());
            //}
        }
    }
}