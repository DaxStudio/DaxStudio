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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DaxStudio.UI.Utils {
    [Export]
    [Export(typeof(ICloseStrategy<IScreen>))]
    public class ApplicationCloseAllStrategy : ICloseStrategy<IScreen> {
        //IEnumerator<IScreen> enumerator;
        //private IEnumerable<IScreen> toclose; 
        //bool finalResult;
        //Action<bool, IEnumerable<IScreen>> callback;
        private IWindowManager _windowManager;
        private IEventAggregator _eventAggregator;

        [ImportingConstructor]
        public ApplicationCloseAllStrategy(IWindowManager windowManager, IEventAggregator eventAggregator )
        {
            _windowManager = windowManager;
            _eventAggregator = eventAggregator;
        }

        public async Task<ICloseResult<IScreen>> ExecuteAsync(IEnumerable<IScreen> toClose, CancellationToken cancellationToken)
        {
            //enumerator = toClose.GetEnumerator();
            var closeable = new List<IScreen>();
            var closeCanOccur = true;

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
                    closeCanOccur = false; // cancel closing
                    closeable.Clear();
                    return new CloseResult<IScreen>(closeCanOccur, closeable);
                }
                else
                {
                    await _eventAggregator.PublishOnUIThreadAsync(new StopAutoSaveTimerEvent());
                    closeCanOccur = await Evaluate(closeable);
                    return new CloseResult<IScreen>(closeCanOccur, closeable);
                }
            }
            else
            {
                //    callback(true, new List<IScreen>());
                await _eventAggregator.PublishOnUIThreadAsync(new StopAutoSaveTimerEvent());
                closeCanOccur = await Evaluate(closeable);
                return new CloseResult<IScreen>(closeCanOccur, closeable);
            }


        }

        async Task<bool> Evaluate(List<IScreen> toclose)
        {
            var finalResult = true;

            //if (!enumerator.MoveNext() || !result) // if last object or something was cancelled
            //    callback(finalResult, new List<IScreen>());
            //else
            //{
            
            foreach (var c in toclose.OfType<IGuardClose>())
            {
                finalResult = finalResult && await c.CanCloseAsync();
            }

                //if (toclose.Any())   
                //{
                //   var tasks =  toclose //conductor.GetChildren()
                //        .OfType<IHaveShutdownTask>()
                //        .Select(x => x.GetShutdownTask())
                //        .Where(x => x != null);

                //    // TODO - show dialog box here 

                //    var sequential = new SequentialResult(tasks.GetEnumerator());
                //    sequential.Completed += (s, e) =>
                //        {
                //            finalresult = finalresult && !e.WasCancelled;
                //        //if(!e.WasCancelled)
                //        //Evaluate(!e.WasCancelled);
                //    };
                //    sequential.Execute(new CoroutineExecutionContext());
                //}

            return finalResult;
            //}
        }
    }
}