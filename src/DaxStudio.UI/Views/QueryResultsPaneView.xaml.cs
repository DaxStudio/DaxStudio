using DaxStudio.Controls;
using DaxStudio.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DaxStudio.UI.Views
{
    /// <summary>
    /// Interaction logic for QueryResultsPaneView.xaml
    /// </summary>
    public partial class QueryResultsPaneView : ZoomableUserControl
    {
        public QueryResultsPaneView()
        {
            InitializeComponent();

            // A DynamicResource set directly on a DataGridColumn (like the SHOW tree's
            // TreeColumn) is not re-evaluated when the theme dictionary is swapped, because
            // columns are not part of the visual tree. Re-apply the theme brushes to the
            // tree-line columns whenever the actual theme changes so the tree lines re-colour
            // on a dark<->light switch instead of keeping a stale (e.g. white) brush.
            ModernWpf.ThemeManager.AddActualThemeChangedHandler(this, OnActualThemeChanged);
            Loaded += (s, e) => ApplyTreeColumnThemeBrushes();
        }

        private void OnActualThemeChanged(object sender, RoutedEventArgs e)
        {
            ApplyTreeColumnThemeBrushes();
        }

        private void ApplyTreeColumnThemeBrushes()
        {
            if (!(TryFindResource("Theme.Brush.Default.Fore") is Brush lineStroke)) return;

            // The SHOW tree-grid is now hosted inside a results tab DataTemplate (there may be zero,
            // one, or several realised instances), so we re-apply the brush to every TreeGrid currently
            // in the visual tree rather than a single named element.
            foreach (var tree in FindVisualChildren<TreeGrid>(this))
            {
                foreach (var column in tree.Columns.OfType<TreeColumn>())
                {
                    column.LineStroke = lineStroke;
                }
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) yield return typed;
                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }
    }
}
