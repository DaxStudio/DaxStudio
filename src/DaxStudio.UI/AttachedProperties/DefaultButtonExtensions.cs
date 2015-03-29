using System;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation.Provider;

namespace DaxStudio.UI.AttachedProperties
{
    public static class DefaultButtonExtensions
    {

        public static readonly DependencyProperty DefaultButtonProperty =
                DependencyProperty.RegisterAttached("DefaultButton",
                                                    typeof(string),
                                                    typeof(DefaultButtonExtensions),
                                                    new PropertyMetadata(DefaultButtonChanged));

        private static void DefaultButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uiElement = d as UIElement;
            var button = e.NewValue as Button;
            if (uiElement != null && button != null)
            {
                uiElement.KeyUp += (sender, arg) =>
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

        public static UIElement GetDefaultButton(UIElement obj)
        {
            return (UIElement)obj.GetValue(DefaultButtonProperty);
        }

        public static void SetDefaultButton(DependencyObject obj, UIElement button)
        {
            obj.SetValue(DefaultButtonProperty, button);
        }
    }


}

