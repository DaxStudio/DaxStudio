using DaxStudio.UI.Extensions;
using System.Reflection;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interactivity;

namespace DaxStudio.UI.Behaviours
{
    public class PasswordBoxBindingBehavior : Behavior<PasswordBox>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PasswordChanged += OnPasswordBoxValueChanged;

            // using _value saved before in OnPropertyChanged
            if (CachedValue != null)
            {
                if (CachedValue.Length == 0)
                    AssociatedObject.Password = string.Empty;
                else
                    AssociatedObject.Password = CachedValue.ConvertToUnsecureString();
            }
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PasswordChanged -= OnPasswordBoxValueChanged;
            base.OnDetaching();
        }

        public SecureString SecurePassword
        {
            get { return (SecureString)GetValue(PasswordProperty); }
            set { SetValue(PasswordProperty, value); }
        }

        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.Register("SecurePassword", typeof(SecureString),
               typeof(PasswordBoxBindingBehavior), new FrameworkPropertyMetadata(OnBoundPasswordChanged));

        private SecureString _cachedValue;
        private bool _valueSet;
        private SecureString CachedValue {
            get => _valueSet? _cachedValue:null; 
            set 
            { 
                if (value != null) _valueSet = true;
                var lengthSet = false;
                try
                {
                    lengthSet = value.Length > 0;
                }
                catch (System.ObjectDisposedException )
                {
                    _valueSet = false;
                }

                _cachedValue = value;
            }
        } 
        
        private bool _skipUpdate;

        private void OnPasswordBoxValueChanged(object sender, RoutedEventArgs e)
        {
            var binding = BindingOperations.GetBindingExpression(this, PasswordProperty);
            if (binding != null)
            {
                PropertyInfo property = binding.DataItem.GetType()
                    .GetProperty(binding.ParentBinding.Path.Path);
                if (property != null)
                    property.SetValue(binding.DataItem, AssociatedObject.SecurePassword, null);
            }
        }

        /// <summary>
        /// Reacts to password reset on viewmodel (ViewModel.Password = new SecureString())
        /// </summary>
        private static void OnBoundPasswordChanged(object s, DependencyPropertyChangedEventArgs e)
        {
            //var box = ((PasswordBoxBindingBehavior)s).AssociatedObject;
            //if (box != null)
            //{
            //    if (((SecureString)e.NewValue).Length == 0)
            //        box.Password = string.Empty;
            //    else
            //        box.Password = ((SecureString)e.NewValue).ConvertToUnsecureString();
            //}
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            base.OnPropertyChanged(e);
            if (AssociatedObject == null)
            {
                // so, let'save the value and then reuse it when OnAttached() called
                CachedValue = e.NewValue as SecureString;
                return;
            }

            if (e.Property == PasswordProperty)
            {
                if (!_skipUpdate)
                {
                    _skipUpdate = true;
                    if (e.NewValue == null)
                    {
                        AssociatedObject.Password = string.Empty;
                    }
                    else
                    {
                        if (((SecureString)e.NewValue).Length == 0)
                            AssociatedObject.Password = string.Empty;
                        else
                            AssociatedObject.Password = ((SecureString)e.NewValue).ConvertToUnsecureString();
                    }
                    _skipUpdate = false;
                }
            }
        }
    }
}
