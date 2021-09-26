using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interactivity;
using System.ComponentModel;

namespace DaxStudio.UI.Triggers
{

    public enum KeyEventAction
    {
        KeyUp,
        KeyDown
    }

    public class KeyPressTrigger : TriggerBase<FrameworkElement>
    {
        public static readonly DependencyProperty TriggerValueProperty = DependencyProperty.Register("TriggerValue", typeof(Key), typeof(KeyPressTrigger), new PropertyMetadata(null));
        public static readonly DependencyProperty KeyActionProperty = DependencyProperty.Register("KeyAction", typeof(KeyEventAction), typeof(KeyPressTrigger), new PropertyMetadata(null));

        [Category("KeyPress Properties")]
        public Key TriggerValue
        {
            get { return (Key)GetValue(TriggerValueProperty); }
            set { SetValue(TriggerValueProperty, value); }
        }

        [Category("KeyPress Properties")]
        public KeyEventAction KeyEvent
        {
            get { return (KeyEventAction)GetValue(KeyActionProperty); }
            set { SetValue(KeyActionProperty, value); }
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            if (KeyEvent == KeyEventAction.KeyUp)
            {
                AssociatedObject.KeyUp += OnKeyPress;
            }
            else if (KeyEvent == KeyEventAction.KeyDown)
            {
                AssociatedObject.KeyDown += OnKeyPress;
            }
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            if (KeyEvent == KeyEventAction.KeyUp)
            {
                AssociatedObject.KeyUp -= OnKeyPress;
            }
            else if (KeyEvent == KeyEventAction.KeyDown)
            {
                AssociatedObject.KeyDown -= OnKeyPress;
            }
        }

        private void OnKeyPress(object sender, KeyEventArgs args)
        {
            if (args.Key.Equals(TriggerValue))
            {
                args.Handled = true;
                InvokeActions(null);
            }
        }
    }
}

