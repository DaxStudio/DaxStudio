using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Model;
using DaxStudio.UI.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DaxStudio.UI.Controls
{
    /// <summary>
    /// Interaction logic for HotKeyEditorControl.xaml
    /// </summary>
    public partial class HotkeyEditorControl : UserControl
    {

        

        private void InitializeValidation()
        {
            Binding binding = BindingOperations.GetBinding(this, HotkeyEditorControl.HotkeyProperty);
            //binding.NotifyOnValidationError = true;
            //binding.ValidatesOnNotifyDataErrors = true;
            binding.ValidationRules.Clear();
            var rule = new HotkeyValidationRule();
            rule.Wrapper = new Wrapper() { Options = (IGlobalOptions)this.DataContext };
            rule.Wrapper.PropertyName = binding.Path.Path;
            rule.Wrapper.HotkeyEditorControl = this;
            rule.ValidationStep = ValidationStep.ConvertedProposedValue;
            binding.ValidationRules.Add(rule);
            //this.Validation.AddErrorHandler
            System.Windows.Controls.Validation.AddErrorHandler(this, HotkeyTextBox_Error);

            //    FrameworkElement SelectedObject = HotkeyTextBox;
            //    SelectedObject.bin

            //    DependencyProperty property =
            //        GetDependencyPropertyByName(SelectedObject, "TextProperty");
            //    Binding binding = new Binding("Model.Txt0");
            //    binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
            //    binding.ValidatesOnDataErrors = true;
            //    RequiredValidate role = new RequiredValidate();
            //    binding.ValidationRules.Add(role);
            //    SelectedObject.SetBinding(property, binding);
        }

        public static readonly DependencyProperty HotkeyProperty =
            DependencyProperty.Register(nameof(Hotkey), typeof(Hotkey), typeof(HotkeyEditorControl),
                new FrameworkPropertyMetadata(default(Hotkey), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public Hotkey Hotkey
        {
            get => (Hotkey)GetValue(HotkeyProperty);
            set => SetValue(HotkeyProperty, value);
        }

        public HotkeyEditorControl()
        {
            InitializeComponent();
            //this.DataContextChanged += HotkeyEditorControl_DataContextChanged;
        }

        //private void HotkeyEditorControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        //{
        //    if (HotkeyTextBox != null)
        //    {
        //        var binding = new Binding();
        //        binding.Source = this.DataContext;
        //        binding.ValidatesOnDataErrors = true;
        //        binding.ValidatesOnExceptions = true;
                
        //        var rule = new HotkeyValidationRule();

                
        //        binding.ValidationRules.Add(rule);
                


        //        binding.Path = new PropertyPath("Hotkey");
        //        HotkeyTextBox.SetBinding(TextBox.TextProperty, binding);
                
        //        //dpMain.Children.Add(_textBox);
        //    }
        //}

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Don't let the event pass further
            // because we don't want standard textbox shortcuts working
            e.Handled = true;

            // Get modifiers and key data
            var modifiers = Keyboard.Modifiers;
            var key = e.Key;

            // When Alt is pressed, SystemKey is used instead
            if (key == Key.System)
            {
                key = e.SystemKey;
            }

            // Pressing delete, backspace or escape without modifiers clears the current value
            if (modifiers == ModifierKeys.None && key.IsEither(Key.Delete, Key.Back, Key.Escape))
            {
                Hotkey = null;
                return;
            }

            // If no actual key was pressed - return
            if (key.IsEither(
                Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt,
                Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin,
                Key.Clear, Key.OemClear, Key.Apps))
            {
                return;
            }

            // Set values
            Hotkey = new Hotkey(key, modifiers);
        }

        private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox txtBox = sender as TextBox;
            //if (e.Action == ValidationErrorEventAction.Added)
            //{
                txtBox.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    //var be = BindingOperations.GetBindingExpressionBase(txtBox, TextBox.TextProperty);
                    var be = BindingOperations.GetBindingExpressionBase(txtBox.Parent, HotkeyEditorControl.HotkeyProperty);
                    be.UpdateTarget();
                    txtBox.ToolTip = null;
                }), System.Windows.Threading.DispatcherPriority.Render);

            //}
            //else
            //{
            //    txtBox.ToolTip = null;
            //}
        }

        private void HotkeyTextBox_Error(object sender, ValidationErrorEventArgs e)
        {
            var txtBox = sender as HotkeyEditorControl;
            if (e.Action == ValidationErrorEventAction.Added)
            {
                txtBox.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    var be = BindingOperations.GetBindingExpressionBase(txtBox, HotkeyEditorControl.HotkeyProperty);
                    be.UpdateTarget();
                    txtBox.ToolTip = e.Error.ErrorContent.ToString();
                }), System.Windows.Threading.DispatcherPriority.Render);
                
            }
            else
            {
                txtBox.ToolTip = null;
            }
        }

        private void HotkeyTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeValidation();
        }
    }


}
