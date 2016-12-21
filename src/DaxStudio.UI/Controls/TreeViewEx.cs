using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DaxStudio.UI.Controls
{
    public static class TVExtensions
    {

        public static T GetChildOfType<T>(this DependencyObject depObj)
        where T : DependencyObject
        {
            if (depObj == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);

                var result = (child as T) ?? GetChildOfType<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }

    // Adds a ScrollBarWidth dependancy property so that we can bind to 
    // it in order to move the animated search icon
    class TreeViewEx: TreeView, INotifyPropertyChanged
    {
        public TreeViewEx()
        {
            LayoutUpdated += TreeViewEx_LayoutUpdated;
            ScrollBarWidth = 20;
        }

        void TreeViewEx_LayoutUpdated(object sender, EventArgs e)
        {
            var sv = this.GetChildOfType<ScrollViewer>();
            if (sv == null) return;
            var vis = sv.ComputedVerticalScrollBarVisibility;
            
            if (vis == System.Windows.Visibility.Collapsed)
                ScrollBarWidth = 0;
            else
                ScrollBarWidth = sv.ActualWidth - sv.ViewportWidth;
        }


        public static readonly DependencyProperty ScrollBarWidthProperty =
            DependencyProperty.Register("ScrollBarWidth", typeof(double), typeof(TreeViewEx)
            , new FrameworkPropertyMetadata(
               0.0, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.Inherits ));
      
        public double ScrollBarWidth
        {
            get { return this.GetValue(ScrollBarWidthProperty) as double? ?? 0 ;}
            set { 
                this.SetValue(ScrollBarWidthProperty,value);
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("ScrollBarWidth"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
      
    }
}
