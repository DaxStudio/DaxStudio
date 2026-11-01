using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using AvalonDock.Controls;

namespace DaxStudio.UI.Controls
{
    public class DocumentTabPanel : DocumentPaneTabPanel
    {
        private static readonly DependencyPropertyKey VisibleTabsWidthPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(VisibleTabsWidth), typeof(double), typeof(DocumentTabPanel),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsParentArrange));

        public static readonly DependencyProperty VisibleTabsWidthProperty = VisibleTabsWidthPropertyKey.DependencyProperty;

        public double VisibleTabsWidth => (double)GetValue(VisibleTabsWidthProperty);

        protected override Size ArrangeOverride(Size finalSize)
        {
            var result = base.ArrangeOverride(finalSize);
            // AvalonDock decides which tabs fit during Arrange, not Measure.
            var tabsRight = 0.0;
            foreach (UIElement tab in Children)
            {
                if (tab.Visibility == Visibility.Visible)
                    tabsRight = Math.Max(tabsRight, LayoutInformation.GetLayoutSlot((FrameworkElement)tab).Right);
            }

            SetValue(VisibleTabsWidthPropertyKey, tabsRight);
            return result;
        }
    }
}
