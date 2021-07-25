using Caliburn.Micro;
using DaxStudio.UI.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interactivity;
using AvalonDock;

namespace DaxStudio.UI.Behaviours
{
    public class AvalonDockLayoutSerializerBehaviour: Behavior<DockingManager>
    {
        public static readonly DependencyProperty LoadSavedLayoutProperty =
            DependencyProperty.RegisterAttached("LoadSavedLayout", typeof(bool), typeof(AvalonDockLayoutSerializerBehaviour),
                                                new PropertyMetadata(OnLoadSavedLayoutCallback));

        public static bool GetLoadSavedLayout(DependencyObject obj)
        {
            return (bool)obj.GetValue(LoadSavedLayoutProperty);
        }

        public static void SetLoadSavedLayout(DependencyObject obj, bool value)
        {
            obj.SetValue(LoadSavedLayoutProperty, value);
        }

        private static void OnLoadSavedLayoutCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //var dm = d as DockingManager;
            //if ((bool)e.NewValue)
            //{
            //    LoadSavedLayout(dm);
            //}
        }

        private static void LoadSavedLayout(DockingManager dockingManager)
        {
            dockingManager.LoadLayout();
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += OnAssociatedObjectLoaded;
        }

        private void OnAssociatedObjectLoaded(object sender, RoutedEventArgs e)
        {
            var dm = sender as DockingManager;
            if (dm == null) return;
            var shouldLoadSavedLayout = (bool)dm.GetValue(LoadSavedLayoutProperty);
            if (shouldLoadSavedLayout) LoadSavedLayout(dm);
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Loaded -= OnAssociatedObjectLoaded;
            base.OnDetaching();

        }
    }
}
