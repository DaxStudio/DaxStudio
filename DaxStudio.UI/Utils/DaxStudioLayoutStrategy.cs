using System.Linq;
using Xceed.Wpf.AvalonDock.Layout;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.Utils
{
    public class DaxStudioLayoutStrategy : ILayoutUpdateStrategy
    {
        public bool BeforeInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableToShow, ILayoutContainer destinationContainer)
        {
            var myViewModel = anchorableToShow.Content as IToolWindow;
            if (myViewModel != null)
            {
                var lap = layout.Descendents();
                var pane = lap.OfType<LayoutAnchorablePane>().FirstOrDefault(d => d.Name == myViewModel.DefaultDockingPane);
                if (pane != null)
                {
                    pane.Children.Add(anchorableToShow);
                    return true;
                }
            }

            return false;
        }

        public void AfterInsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableShown)
        {
            
        }

        public bool BeforeInsertDocument(LayoutRoot layout, LayoutDocument anchorableToShow, ILayoutContainer destinationContainer)
        {
            //TODO - can I stop anchorables being docked?
            return false;
        }

        public void AfterInsertDocument(LayoutRoot layout, LayoutDocument anchorableShown)
        {
            
        }

        public bool InsertAnchorable(LayoutRoot layout, LayoutAnchorable anchorableToShow, ILayoutContainer destinationContainer)
        {
            return false;
        }
    }
}
