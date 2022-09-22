using Caliburn.Micro;
using DaxStudio.UI.ViewModels;
using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DaxStudio.UI.Extensions
{
    
    public static class WindowManagerExtensions
    {
        /*
        public static MessageBoxResult ShowMetroMessageBox(this IWindowManager @this, string message, string title, MessageBoxButton buttons)
        {
            MessageBoxResult retval;
            var shellViewModel = IoC.Get<ShellViewModel>();

            try
            {
                shellViewModel.ShowOverlay();
                var model = new MetroMessageBoxViewModel(message, title, buttons);
                @this.ShowDialog(model);

                retval = model.Result;
            }
            finally
            {
                shellViewModel.HideOverlay();
            }

            return retval;
        }

        public static void ShowMetroMessageBox(this IWindowManager @this, string message)
        {
            @this.ShowMetroMessageBox(message, "System Message", MessageBoxButton.OK);
        }
        */
        public static async Task ShowDialogBoxAsync<T>(this IWindowManager @this, Dictionary<string,object> settings)
        {

            var shellViewModel = IoC.Get<IShell>();

            try
            {
                shellViewModel.ShowOverlay();
                var model = IoC.Get<T>();
                await @this.ShowDialogAsync(model,null,settings);

            }

            finally
            {
                shellViewModel.HideOverlay();
            }

        }

        public static async Task ShowDialogBoxAsync(this IWindowManager @this, object model)
        {
            Dictionary<string, object> settings = new Dictionary<string, object>();
            await ShowDialogBoxAsync(@this,model, settings);
        }
            public static async Task ShowDialogBoxAsync(this IWindowManager @this, object model, Dictionary<string,object> settings)
        {

            var shellViewModel = IoC.Get<IShell>();

            try
            {
                shellViewModel.ShowOverlay();
                EnsureStandardSettings(settings);
                await @this.ShowDialogAsync(model, null, settings);

            }
            finally
            {
                shellViewModel.HideOverlay();
            }

            //var view = ViewLocator.LocateForModel(model, null, null) as ContentDialog;
            //if (view == null) throw new ArgumentException($"The view for the ViewModel '{model.ToString()}' is not a ModernWpf ContentDialog control");

            //ViewModelBinder.Bind(model, view, null);
            //view.ShowAsync();
            // ?? TODO - how to pass dialog result back
        }

        public static void EnsureStandardSettings(Dictionary<string,object> settings)
        {
            if (!settings.ContainsKey("ShowInTaskbar")) { settings.Add("ShowInTaskbar", false); }
            if (!settings.ContainsKey("ResizeMode")) { settings.Add("ResizeMode", ResizeMode.NoResize); }
            if (!settings.ContainsKey("WindowStyle")) { settings.Add("WindowStyle", WindowStyle.None); }
            if (!settings.ContainsKey("Background")) { settings.Add("Background", System.Windows.Media.Brushes.Transparent); }
            if (!settings.ContainsKey("AllowsTransparency")) { settings.Add("AllowsTransparency", true); }
            if (!settings.ContainsKey("Style")) { settings.Add("Style", null); }
        }

        public static async Task<bool> ShowContentDialogAsync(this IWindowManager @this, object model, Dictionary<string, object> settings)
        {

            //var shellViewModel = IoC.Get<IShell>();

            //try
            //{
            //    shellViewModel.ShowOverlay();

            //    @this.ShowDialogAsync(model, null, settings);

            //}
            //finally
            //{
            //    shellViewModel.HideOverlay();
            //}

            var view = ViewLocator.LocateForModel(model, null, null) as ContentDialog;
            if (view == null) throw new ArgumentException($"The view for the ViewModel '{model.ToString()}' is not a ModernWpf ContentDialog control");

            ViewModelBinder.Bind(model, view, null);
            var result = await view.ShowAsync();

            return result == ContentDialogResult.Primary;
        }

    }

}
