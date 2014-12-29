using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace DaxStudio.UI.AttachedProperties
{
    public  class DefaultButtonProp:DependencyObject
    {
        public static UIElement GetButton(DependencyObject obj)
        {
            return (UIElement)obj.GetValue(DefaultButtonProperty);
        }

        public static void SetButton(DependencyObject obj, UIElement value)
        {
            obj.SetValue(DefaultButtonProperty, value);
        }
        public static readonly DependencyProperty DefaultButtonProperty =
            DependencyProperty.RegisterAttached("Button", 
                    typeof(UIElement), 
                    typeof(DefaultButtonProp), 
                    new UIPropertyMetadata(null, OnDefaultButtonChanged));

        private static void OnDefaultButtonChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            var uiElement = args.NewValue as UIElement;
            var button = args.NewValue as Button;
            if (uiElement != null && button != null)
            {
                uiElement.KeyUp += (sender2, arg) =>
                {
                    var peer = new ButtonAutomationPeer(button);

                    if (arg.Key == Key.Enter)
                    {
                        peer.SetFocus();
                        uiElement.Dispatcher.BeginInvoke((Action)delegate
                        {

                            var invokeProv =
                                peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                            if (invokeProv != null)
                                invokeProv.Invoke();
                        });
                    }
                };
            }
        }

        private static void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
