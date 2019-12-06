using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interactivity;

namespace DaxStudio.UI.Behaviours
{
    class ResetInvalidTextBehaviour: Behavior<TextBox>
    {
        
        //private string ProperValue = String.Empty;
        protected override void OnAttached()
        {
            AssociatedObject.LostFocus += AssociatedObjectOnLostFocus;
            
            //AssociatedObject.PreviewKeyDown += AssociatedObject_PreviewKeyDown;
            //AssociatedObject.GotFocus += AssociatedObject_GotFocus;
            base.OnAttached();
        }

        //void AssociatedObject_GotFocus(object sender, RoutedEventArgs e)
        //{
        //    TextBox tb = sender as TextBox;
        //    ProperValue = tb.Text;
        //}

        //void AssociatedObject_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        //{
        //    if (e.Key == Key.Enter)
        //    {
        //        TextBox tb = sender as TextBox;
        //        ProperValue = tb.Text;
        //    }
        //}

        protected override void OnDetaching()
        {
            AssociatedObject.LostFocus -= AssociatedObjectOnLostFocus;
            //AssociatedObject.PreviewKeyDown -= AssociatedObject_PreviewKeyDown;
            //AssociatedObject.GotFocus -= AssociatedObject_GotFocus;
            base.OnDetaching();
        }

        private void AssociatedObjectOnLostFocus(object sender, RoutedEventArgs routedEventArgs)
        {
            TextBox tb = sender as TextBox;
            //tb.Text = ProperValue;
            tb.Dispatcher.BeginInvoke(new Action(() =>
            {
                var be = BindingOperations.GetBindingExpressionBase(tb, TextBox.TextProperty);
                be.UpdateTarget();
                //tb.ToolTip = e.Error.ErrorContent.ToString();
            }), System.Windows.Threading.DispatcherPriority.Render);

        }
    }

    
}
