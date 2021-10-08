using Caliburn.Micro;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Extensions
{
    public class DaxStudioWindowManager:WindowManager
    {

        public override Task<bool?> ShowDialogAsync(object rootModel, object context = null, IDictionary<string, object> settings = null)
        {
            return base.ShowDialogAsync(rootModel, context, settings);
        }

        //public async Task<bool?> ShowDialogAsync(object rootModel, object context = null, IDictionary<string, object> settings = null)
        //{
        //    var dialog = CreateDialog(rootModel, context, settings);
        //    var result = await dialog.ShowAsync();
            
        //    return result == ContentDialogResult.Primary;
        //}

        
        /// <summary>
        /// Creates a window.
        /// </summary>
        /// <param name="rootModel">The view model.</param>
        /// <param name="isDialog">Whethor or not the window is being shown as a dialog.</param>
        /// <param name="context">The view context.</param>
        /// <param name="settings">The optional popup settings.</param>
        /// <returns>The window.</returns>
        protected virtual ContentDialog CreateDialog(object rootModel, object context, IDictionary<string, object> settings)
        {
            var view =  ViewLocator.LocateForModel(rootModel, null, context) as ContentDialog;
            if (view == null) throw new ArgumentException($"The view for the ViewModel '{rootModel.ToString()}' is not a ModernWpf ContentDialog control");

            ViewModelBinder.Bind(rootModel, view, context);

            //var haveDisplayName = rootModel as IHaveDisplayName;
            //if (string.IsNullOrEmpty(view.Title) && haveDisplayName != null && !ConventionManager.HasBinding(view, Window.TitleProperty))
            //{
            //    var binding = new Binding("DisplayName") { Mode = BindingMode.TwoWay };
            //    view.SetBinding(Window.TitleProperty, binding);
            //}

            //ApplySettings(view, settings);

            //var conductor = new WindowConductor(rootModel, view);

            //await conductor.InitialiseAsync();

            return view;
        }

        
    }
}
