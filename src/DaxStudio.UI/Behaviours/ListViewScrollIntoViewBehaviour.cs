using System.Windows.Controls;
using System.Windows.Interactivity;

namespace DaxStudio.UI.Behaviours {

    class ListViewScrollIntoViewBehavior : Behavior<ListView>
    {
        protected override void OnAttached()
        {
            ListView listView = AssociatedObject;
            listView.SelectionChanged += OnListBox_SelectionChanged;
        }

        private void OnListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListView listView = AssociatedObject;
            listView.ScrollIntoView(listView.SelectedItem);
        }

        protected override void OnDetaching()
        {
            ListView listView = AssociatedObject;
            listView.SelectionChanged -= OnListBox_SelectionChanged;
        }

    }
}