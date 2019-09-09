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
            AssociatedObject.PasswordChanged += OnPasswordBoxValueChanged;
        }

        public SecureString SecurePassword
        {
            get { return (SecureString)GetValue(PasswordProperty); }
            set { SetValue(PasswordProperty, value); }
        }

        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.Register("SecurePassword", typeof(SecureString),
               typeof(PasswordBoxBindingBehavior), new FrameworkPropertyMetadata(OnBoundPasswordChanged));


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
            var box = ((PasswordBoxBindingBehavior)s).AssociatedObject;
            if (box != null)
            {
                if (((SecureString)e.NewValue).Length == 0)
                    box.Password = string.Empty;
                else
                    box.Password = ((SecureString)e.NewValue).ConvertToUnsecureString();
            }
        }

    }
}
