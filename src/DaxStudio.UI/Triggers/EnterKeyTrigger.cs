﻿using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace DaxStudio.UI.Triggers
{
    public class EnterKeyTrigger : TriggerBase<UIElement>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.KeyUp += AssociatedObject_KeyUp;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.KeyUp -= AssociatedObject_KeyUp;
        }

        void AssociatedObject_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                //TextBox textBox = AssociatedObject as TextBox;
                //object o = textBox == null ? null : textBox.Text;
                //if (o != null)
                if (AssociatedObject != null)
                {
                    InvokeActions(AssociatedObject);
                }
            }
        }
    }
}
