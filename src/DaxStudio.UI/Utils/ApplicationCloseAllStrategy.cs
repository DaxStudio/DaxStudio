using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;

namespace DaxStudio.UI.Utils {
    [Export]
    [Export(typeof(ICloseStrategy<IScreen>))]
    public class ApplicationCloseAllStrategy : ICloseStrategy<IScreen> {
        //IEnumerator<IScreen> enumerator;
        private IEnumerable<IScreen> toclose; 
        bool finalResult;
        Action<bool, IEnumerable<IScreen>> callback;
        private IWindowManager _windowManager;
        private IEventAggregator _eventAggregator;

        [ImportingConstructor]
        public ApplicationCloseAllStrategy(IWindowManager windowManager, IEventAggregator eventAggregator )
        {
            _windowManager = windowManager;
            _eventAggregator = eventAggregator;
        }

        public void Execute(IEnumerable<IScreen> toClose, Action<bool, IEnumerable<IScreen>> callback)
        {
            //enumerator = toClose.GetEnumerator();
            this.toclose = toClose;
            this.callback = callback;

            var docs = toClose.Cast<ISaveable>();// .ConvertAll(o => (DocumentViewModel)o);
            var dirtyDocs = new ObservableCollection<ISaveable>(docs.Where<ISaveable>(x => x.IsDirty));

            if (dirtyDocs.Count > 0)
            {
                var closeDialog = new SaveDialogViewModel();
                closeDialog.Documents = dirtyDocs;

                _windowManager.ShowDialogBox(closeDialog, settings: new Dictionary<string, object>
                {
                    { "WindowStyle", WindowStyle.None},
                    { "ShowInTaskbar", false},
                    { "ResizeMode", ResizeMode.NoResize},
                    { "Background", System.Windows.Media.Brushes.Transparent},
                    { "AllowsTransparency",true}
                
                });

                if (closeDialog.Result == Enums.SaveDialogResult.Cancel)
                {
                    // loop through and cancel closing for any dirty documents
                    finalResult = false; // cancel closing
                    Evaluate(finalResult);
                    
                }
                else
                {
                    _eventAggregator.PublishOnUIThread(new StopAutoSaveTimerEvent());
                    Evaluate(finalResult); 
                }
            }
            else
            {
                //    callback(true, new List<IScreen>());
                _eventAggregator.PublishOnUIThread(new StopAutoSaveTimerEvent());
                Evaluate(finalResult);

            }


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