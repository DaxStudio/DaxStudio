using DaxStudio.Interfaces;
using DaxStudio.Interfaces.Attributes;
using DaxStudio.UI.Controls;
using System;
using System.Collections.Generic;

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DaxStudio.UI.Validation
{
    public class Wrapper : DependencyObject
    {
        public static readonly DependencyProperty OptionsProperty =
             DependencyProperty.Register("Options", typeof(IGlobalOptions),
             typeof(Wrapper), null);

        public IGlobalOptions Options
        {
            get { return (IGlobalOptions)GetValue(OptionsProperty); }
            set { SetValue(OptionsProperty, value); }
        }

        public static readonly DependencyProperty PropertyNameProperty =
             DependencyProperty.Register("PropertyName", typeof(string),
             typeof(Wrapper));

        public string PropertyName
        {
            get => (string)GetValue(PropertyNameProperty);
            set => SetValue(PropertyNameProperty, value);
        }

        public static readonly DependencyProperty HotkeyEditorControlProperty =
             DependencyProperty.Register("HotkeyEditorControl", typeof(HotkeyEditorControl),
             typeof(Wrapper));

        public HotkeyEditorControl HotkeyEditorControl
        {
            get => (HotkeyEditorControl)GetValue(HotkeyEditorControlProperty);
            set => SetValue(HotkeyEditorControlProperty, value);
        }


    }

    public class HotkeyValidationRule : System.Windows.Controls.ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            string hotkey = value?.ToString()??string.Empty;
            var msg = string.Empty;
            var props = this.Wrapper.Options.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var prop in props)
            {
                var att = prop.GetCustomAttributes(typeof(HotkeyAttribute),true);
                if (att.Length == 0) continue;
                if (prop.Name == this.Wrapper.PropertyName) continue;
                if ((string)prop.GetValue(this.Wrapper.Options) == hotkey) {
                    msg = $"Cannot add Duplicate Hotkey '{hotkey}'";
                    this.Wrapper.Options.HotkeyWarningMessage = msg;
                    // rollback to original value
                    BindingOperations.GetBindingExpressionBase(
                        ((Control)this.Wrapper.HotkeyEditorControl), HotkeyEditorControl.HotkeyProperty).UpdateTarget();
                    
                    return new ValidationResult(false, msg);
                }
            }

            this.Wrapper.Options.HotkeyWarningMessage = msg;

            return ValidationResult.ValidResult;
        }

        public Wrapper Wrapper { get; set; }
        
    }
}
