using Caliburn.Micro;
using System;
using System.Diagnostics.Contracts;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace DaxStudio.UI.Extensions
{
    public static class ViewAwareExtensions
    {
        
            public static bool SetFocus(this IViewAware screen, Expression<Func<object>> propertyExpression)
            {
                return SetFocus(screen, propertyExpression.GetMemberInfo().Name);
            }

            public static bool SetFocus(this IViewAware screen, string property)
            {
                Contract.Requires(property != null, "Property cannot be null.");
                var view = screen.GetView() as UserControl;
                if (view != null)
                {
                    var control = FindChild(view, property);
                    bool focus = control != null && control.Focus();
                    return focus;
                }
                return false;
            }

            private static FrameworkElement FindChild(UIElement parent, string childName)
            {
                // Confirm parent and childName are valid. 
                if (parent == null || string.IsNullOrWhiteSpace(childName)) return null;

                FrameworkElement foundChild = null;

                int childrenCount = VisualTreeHelper.GetChildrenCount(parent);

                for (int i = 0; i < childrenCount; i++)
                {
                    FrameworkElement child = VisualTreeHelper.GetChild(parent, i) as FrameworkElement;
                    if (child != null)
                    {

                        BindingExpression bindingExpression = GetBindingExpression(child);
                        if (child.Name == childName)
                        {
                            foundChild = child;
                            break;
                        }
                        if (bindingExpression != null)
                        {
                            if (bindingExpression.ResolvedSourcePropertyName == childName)
                            {
                                foundChild = child;
                                break;
                            }
                        }
                        foundChild = FindChild(child, childName);
                        if (foundChild != null)
                        {
                            if (foundChild.Name == childName)
                                break;
                            BindingExpression foundChildBindingExpression = GetBindingExpression(foundChild);
                            if (foundChildBindingExpression != null &&
                                foundChildBindingExpression.ResolvedSourcePropertyName == childName)
                                break;
                        }

                    }
                }

                return foundChild;
            }

            private static BindingExpression GetBindingExpression(FrameworkElement control)
            {
                if (control == null) return null;

                BindingExpression bindingExpression = null;
                var convention = ConventionManager.GetElementConvention(control.GetType());
                if (convention != null)
                {
                    var bindablePro = convention.GetBindableProperty(control);
                    if (bindablePro != null)
                    {
                        bindingExpression = control.GetBindingExpression(bindablePro);
                    }
                }
                return bindingExpression;
            }
        }
    
}
