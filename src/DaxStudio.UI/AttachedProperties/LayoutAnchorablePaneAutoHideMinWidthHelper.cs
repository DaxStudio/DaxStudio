using AvalonDock.Layout;
using System.Windows;

namespace DaxStudio.UI.AttachedProperties
{
    public static class LayoutAnchorablePaneAutoHideMinWidthHelper
    {

        private static void AutoHideMinWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as LayoutAnchorablePane;
            if (element == null) return;
            element.ChildrenCollectionChanged += Element_ChildrenCollectionChanged;
        }

        private static void Element_ChildrenCollectionChanged(object sender, System.EventArgs e)
        {
            var element = sender as LayoutAnchorablePane;
            if (element == null) return;
            var newValue = double.Parse( GetAutoHideMinWidth(element).ToString() );
            foreach (var child in element.Children)
            {
                if (child is LayoutAnchorable anchorable)
                {
                    anchorable.AutoHideMinWidth = (double)newValue;
                }
            }
        }

        public static readonly DependencyProperty AutoHideMinWidthProperty = DependencyProperty.RegisterAttached("AutoHideMinWidth",
            typeof(object),
            typeof(LayoutAnchorablePaneAutoHideMinWidthHelper),
            new PropertyMetadata((double)100, AutoHideMinWidthChanged));

        public static void SetAutoHideMinWidth(LayoutAnchorablePane element, object value)
        {
            element.SetValue(AutoHideMinWidthProperty, value);
        }

        public static object GetAutoHideMinWidth(LayoutAnchorablePane element)
        {
            return element.GetValue(AutoHideMinWidthProperty);
        }
    }
}
