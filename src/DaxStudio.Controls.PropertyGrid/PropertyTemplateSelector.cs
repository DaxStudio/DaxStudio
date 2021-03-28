using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.Controls.PropertyGrid
{
    public class PropertyTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            FrameworkElement element = container as FrameworkElement;

            if (element != null && item != null && item is PropertyBinding<object>)
            {
                PropertyBinding<object> prop = item as PropertyBinding<object>;

                var templateDictionary = SetupTemplateDictionary(element);

                if (prop.PropertyType.IsEnum) return element.FindResource("EnumTemplate") as DataTemplate;
                if (prop.PropertyType == typeof(bool)) return  element.FindResource("BoolTemplate") as DataTemplate;
                if (prop.PropertyType == typeof(double)) return element.FindResource("DoubleTemplate") as DataTemplate;
                if (prop.PropertyType == typeof(int)) return element.FindResource("IntegerTemplate") as DataTemplate;
                if (prop.DisplayName.EndsWith("Font Family")) return element.FindResource("FontFamilyTemplate") as DataTemplate;
                if (prop.DisplayName.EndsWith("Password")) return element.FindResource("PasswordTemplate") as DataTemplate;
                return element.FindResource("GenericTemplate") as DataTemplate;
                
            }

            return null;
        }

        
        public static Dictionary<Type,Func<DataTemplate>> SetupTemplateDictionary(FrameworkElement element)
        {
            var TemplateDictionary = new Dictionary<Type, Func<DataTemplate>>();
            TemplateDictionary.Add(typeof(bool), () => element.FindResource("BoolTemplate") as DataTemplate);
            TemplateDictionary.Add(typeof(string), () => element.FindResource("GenericTemplate") as DataTemplate);
            TemplateDictionary.Add(typeof(Enum), () => element.FindResource("EnumTemplate") as DataTemplate);
            return TemplateDictionary;
        }
    }
}
