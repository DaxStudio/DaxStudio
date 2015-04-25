using System;
using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.Utils {
    public class ApplicationCloseStrategy : ICloseStrategy<IScreen> {
        //IEnumerator<IScreen> enumerator;
        private IEnumerable<IScreen> toclose; 
        bool finalResult;
        Action<bool, IEnumerable<IScreen>> callback;

        public void Execute(IEnumerable<IScreen> toClose, Action<bool, IEnumerable<IScreen>> callback)
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

                    var sequential = new SequentialResult(tasks.GetEnumerator());
                    sequential.Completed += (s, e) =>
                        {
                            callback(!e.WasCancelled, new List<IScreen>());
                        //if(!e.WasCancelled)
                        //Evaluate(!e.WasCancelled);
                    };
                    sequential.Execute(new CoroutineExecutionContext());
                }
                else callback(true, new List<IScreen>());
            //}
        }
    }
}