using Caliburn.Micro;
using DaxStudio.UI.ViewModels;
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
        public static void ShowDialogBox<T>(this IWindowManager @this, Dictionary<string,object> settings)
        {

            var shellViewModel = IoC.Get<IShell>();

            try
            {
                shellViewModel.ShowOverlay();
                var model = IoC.Get<T>();
                @this.ShowDialog(model,null,settings);

            }

            finally
            {
                shellViewModel.HideOverlay();
            }

        }

        public static void ShowDialogBox(this IWindowManager @this, object model, Dictionary<string,object> settings)
        {

            var shellViewModel = IoC.Get<IShell>();

            try
            {
                shellViewModel.ShowOverlay();
                
                @this.ShowDialog(model,null,settings);

            }
            finally
            {
                shellViewModel.HideOverlay();
            }

        }

    }


}
