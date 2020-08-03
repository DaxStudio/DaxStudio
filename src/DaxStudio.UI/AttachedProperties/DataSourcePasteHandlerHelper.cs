using DaxStudio.UI.ViewModels;
using DaxStudio.UI.Views;
using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DaxStudio.UI.AttachedProperties
{
    public class DataSourcePasteHandlerHelper //: Behavior<ConnectionDialogView>
    {
        
        public static readonly DependencyProperty PasteHandlerProperty =
            DependencyProperty.RegisterAttached("PasteHandler",
            typeof(DataObjectPastingEventHandler), typeof(DataSourcePasteHandlerHelper),
            new FrameworkPropertyMetadata(null, new PropertyChangedCallback(PasteHandlerPropertyChanged)));

        private static void PasteHandlerPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            ComboBox cb = obj as ComboBox;
            DataObjectPastingEventHandler handler = e.NewValue as DataObjectPastingEventHandler;
            if (cb != null && handler != null)
            {
                DataObject.AddPastingHandler(cb, handler);
            }
        }

        public static void SetPasteHandler(UIElement element, DataObjectPastingEventHandler value)
        {
            element.SetValue(PasteHandlerProperty, value);
        }

        public static DataObjectPastingEventHandler GetPasteHandler(UIElement element)
        {
            return (DataObjectPastingEventHandler)element.GetValue(PasteHandlerProperty);
        }
        

    }
}
