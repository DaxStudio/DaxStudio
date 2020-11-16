using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace DaxStudio.UI.AttachedProperties
{
   

    /// <summary>
    /// This class contains a few useful extenders for the ListBox
    /// </summary>
    public class ListViewExtenders : DependencyObject
    {
        public static readonly DependencyProperty AutoScrollToEndProperty = DependencyProperty.RegisterAttached("AutoScrollToEnd", typeof(bool), typeof(ListViewExtenders), new UIPropertyMetadata(default(bool), OnAutoScrollToEndChanged));

        /// <summary>
        /// Returns the value of the AutoScrollToEndProperty
        /// </summary>
        /// <param name="obj">The dependency-object whichs value should be returned</param>
        /// <returns>The value of the given property</returns>
        public static bool GetAutoScrollToEnd(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoScrollToEndProperty);
        }

        /// <summary>
        /// Sets the value of the AutoScrollToEndProperty
        /// </summary>
        /// <param name="obj">The dependency-object whichs value should be set</param>
        /// <param name="value">The value which should be assigned to the AutoScrollToEndProperty</param>
        public static void SetAutoScrollToEnd(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoScrollToEndProperty, value);
        }

        /// <summary>
        /// This method will be called when the AutoScrollToEnd
        /// property was changed
        /// </summary>
        /// <param name="s">The sender (the ListBox)</param>
        /// <param name="e">Some additional information</param>
        public static void OnAutoScrollToEndChanged(DependencyObject s, DependencyPropertyChangedEventArgs e)
        {
            if (!(s is ListView listView)) return;
            var listViewItems = listView.Items;
            var data = listViewItems.SourceCollection as INotifyCollectionChanged;

            var scrollToEndHandler = new NotifyCollectionChangedEventHandler(
                (s1, e1) =>
                {
                    if (listView.Items.Count <= 0 || e1?.NewItems == null) return;
                    try
                    {
                        //object lastItem = listView.Items[listView.Items.Count - 1];
                        var lastItem = e1.NewItems[0];
                        listView.Items.MoveCurrentTo(lastItem);
                        listView.ScrollIntoView(lastItem);
                        listView.SelectedItem = lastItem;
                    }
                    catch
                    {
                        // swallow any exceptions
                    }
                });

            var gotFocusHandler = new RoutedEventHandler (
                (s2, e2) =>
                {
                    if (listView.Items.Count > 0)
                    {
                        //object lastItem = listView.Items[listView.Items.Count - 1];
                        listView.ScrollIntoView(listView.SelectedItem);
                    }
                });

            if ((bool)e.NewValue)
            {
                if (data != null)
                {
                    data.CollectionChanged += scrollToEndHandler;
                    listView.GotFocus += gotFocusHandler;
                }
            }
            else if (data != null) 
            {
                data.CollectionChanged -= scrollToEndHandler;
                listView.GotFocus -= gotFocusHandler;
            }
        }
    }
}
