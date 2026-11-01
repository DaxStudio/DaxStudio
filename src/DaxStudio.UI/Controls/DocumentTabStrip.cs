using System;
using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.UI.Controls
{
    public class DocumentTabStrip : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            if (InternalChildren.Count != 2) return new Size();

            var tabs = InternalChildren[0];
            var button = InternalChildren[1];
            button.Measure(availableSize);
            tabs.Measure(new Size(Math.Max(0, availableSize.Width - button.DesiredSize.Width), availableSize.Height));

            return new Size(Math.Min(availableSize.Width, tabs.DesiredSize.Width + button.DesiredSize.Width),
                Math.Max(tabs.DesiredSize.Height, button.DesiredSize.Height));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (InternalChildren.Count != 2) return finalSize;

            var tabs = (DocumentTabPanel)InternalChildren[0];
            var button = InternalChildren[1];
            var tabWidth = Math.Max(0, finalSize.Width - button.DesiredSize.Width);
            tabs.Arrange(new Rect(0, 0, tabWidth, finalSize.Height));
            button.Arrange(new Rect(Math.Min(tabs.VisibleTabsWidth, tabWidth), 0,
                Math.Min(button.DesiredSize.Width, finalSize.Width), finalSize.Height));
            return finalSize;
        }
    }
}
