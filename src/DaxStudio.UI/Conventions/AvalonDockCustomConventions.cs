using System;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using AvalonDock;
using AvalonDock.Layout;
using Caliburn.Micro;

namespace DaxStudio.UI
{


    public static class AvalonDockConventions
    {
        public static void Install()
        {
            ConventionManager.AddElementConvention<DockingManager>(DockingManager.DocumentsSourceProperty, "DocumentsSource", "ActiveContent")
                    .ApplyBinding = (viewModelType, path, property, element, convention) =>
                    {
                        if (!ConventionManager.SetBindingWithoutBindingOrValueOverwrite(viewModelType, path, 
                                                                                        property, element,
                                                                                        convention, 
                                                                                        DockingManager.DocumentsSourceProperty))
                        {
                            return false;
                        }

                        var tabControl = (DockingManager)element;
                        if (tabControl.LayoutItemTemplate == null
                            && tabControl.LayoutItemTemplateSelector == null
                            && property.PropertyType.IsGenericType)
                        {
                            var itemType = property.PropertyType.GetGenericArguments().First();
                            if (!itemType.IsValueType && !typeof(string).IsAssignableFrom(itemType))
                                tabControl.LayoutItemTemplate = AvalonDockItemTemplate;
                        }

                        //SelectedItem for AvalonDock is ActiveContent
                        ConventionManager.ConfigureSelectedItem(element,
                                                                DockingManager.ActiveContentProperty,
                                                                viewModelType,
                                                                path);

                        //There is no DisplayMemberPath

                        //Original, for reference
                        //ConventionManager.ApplyHeaderTemplate(tabControl,
                        //                                      DockingManager.DocumentHeaderTemplateProperty,
                        //                                      DockingManager.DocumentHeaderTemplateSelectorProperty,
                        //                                      viewModelType);

                        //CUSTOM ApplyHeaderTemplate()
                        //README
                        //AvalonDock's generic.xaml uses a Setter to provide a default HeaderTemplate
                        //so tabControl.GetValue() will always return a value.
                        //If one is manually added by User in XAML, I cannot find a way of detecting whether the generic default has
                        //been overridden by the one in XAML. Thus we ignore all, and add the one from here.

                        //To enable Tab Closing via XAML, there is a Template
                        //in TabControlOnView.xaml, and one is not set here.
#if false

                        //var template = tabControl.GetValue(DockingManager.DocumentHeaderTemplateProperty) as DataTemplate;
                        var template = tabControl.DocumentHeaderTemplate;

                        template = null;

                        object selector = null;
                        if (DockingManager.DocumentHeaderTemplateSelectorProperty != null)
                        {
                            //selector = tabControl.GetValue(DockingManager.DocumentHeaderTemplateSelectorProperty);
                            selector = tabControl.DocumentHeaderTemplateSelector;
                        }

                        if (template != null || selector != null || !typeof(IHaveDisplayName).IsAssignableFrom(viewModelType))
                        {
                            return false;
                        }

                        //tabControl.SetValue(DockingManager.DocumentHeaderTemplateProperty, AvalonDockHeaderTemplate);
                        tabControl.DocumentHeaderTemplate = AvalonDockHeaderTemplate;
#endif
                        return true;
                    };
        }

        /// <summary>
        /// The default DataTemplate used for ItemsControls when required.
        /// </summary>
        public static DataTemplate AvalonDockItemTemplate = (DataTemplate)
            XamlReader.Parse(
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
                          "xmlns:cal='clr-namespace:Caliburn.Micro;assembly=Caliburn.Micro.Platform'> " +
                "<ContentControl cal:View.Model=\"{Binding .}\" VerticalContentAlignment=\"Stretch\" HorizontalContentAlignment=\"Stretch\" IsTabStop=\"False\" />" +
            "</DataTemplate>"
                     );

        /// <summary>
        /// The default Header DataTemplate for AvalonDock.
        /// NOTE it must be "Content.DisplayName", as the
        /// object on which the template is applied is AvalonDock.LayoutDocument
        /// This Template does not support Tab Closing button
        /// </summary>
        public static DataTemplate AvalonDockHeaderTemplate = (DataTemplate)
            XamlReader.Parse(
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'> " +
                "<TextBlock Text=\"{Binding Content.DisplayName}\" />" +
            "</DataTemplate>"
                    );
 

    }
}
