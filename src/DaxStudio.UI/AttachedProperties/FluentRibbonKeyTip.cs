using Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DaxStudio.UI.AttachedProperties
{
    internal class FluentRibbonAttachedProperties
    {
        
        private static DependencyProperty KeyTipProperty =
            DependencyProperty.RegisterAttached("KeyTip", typeof(string),
                typeof(FluentRibbonAttachedProperties), new PropertyMetadata(default(string), OnKeyTipChanged));

        private static void OnKeyTipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = d as IKeyTipedControl;
            if (ctrl == null)
                return;

            ctrl.KeyTip = e.NewValue.ToString();
        }

        public static string GetKeyTip(DependencyObject dp)
        {
            if (dp == null) throw new ArgumentNullException("dp");

            return (string)dp.GetValue(KeyTipProperty);
        }

        public static void SetKeyTip(DependencyObject dp, object value)
        {
            if (dp == null) throw new ArgumentNullException("dp");

            dp.SetValue(KeyTipProperty, value);
        }
    }



}
