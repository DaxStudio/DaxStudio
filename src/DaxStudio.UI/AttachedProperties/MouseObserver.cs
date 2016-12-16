using System.Windows;
using System.Windows.Input;

namespace DaxStudio.UI.AttachedProperties
{
    public static class MouseObserver
    {
        public static readonly DependencyProperty ObserveProperty = DependencyProperty.RegisterAttached(
            "Observe",
            typeof(bool),
            typeof(MouseObserver),
            new FrameworkPropertyMetadata(OnObserveChanged));

        public static readonly DependencyProperty ObservedMouseOverProperty = DependencyProperty.RegisterAttached(
            "ObservedMouseOver",
            typeof(bool),
            typeof(MouseObserver));


        public static bool GetObserve(FrameworkElement frameworkElement)
        {
            return (bool)frameworkElement.GetValue(ObserveProperty);
        }

        public static void SetObserve(FrameworkElement frameworkElement, bool observe)
        {
            frameworkElement.SetValue(ObserveProperty, observe);
        }

        public static double GetObservedMouseOver(FrameworkElement frameworkElement)
        {
            return (double)frameworkElement.GetValue(ObservedMouseOverProperty);
        }

        public static void SetObservedMouseOver(FrameworkElement frameworkElement, double observedMouseOver)
        {
            frameworkElement.SetValue(ObservedMouseOverProperty, observedMouseOver);
        }

        private static void OnObserveChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var frameworkElement = (FrameworkElement)dependencyObject;
            if ((bool)e.NewValue)
            {
                frameworkElement.MouseEnter += OnFrameworkElementMouseOverChanged;
                frameworkElement.MouseLeave += OnFrameworkElementMouseOverChanged;
                UpdateObservedMouseOverForFrameworkElement(frameworkElement);
            }
            else
            {
                frameworkElement.MouseEnter -= OnFrameworkElementMouseOverChanged;
                frameworkElement.MouseLeave -= OnFrameworkElementMouseOverChanged;
            }
        }

        private static void OnFrameworkElementMouseOverChanged(object sender, MouseEventArgs e)
        {
            UpdateObservedMouseOverForFrameworkElement((FrameworkElement)sender);
        }

        private static void UpdateObservedMouseOverForFrameworkElement(FrameworkElement frameworkElement)
        {
            frameworkElement.SetCurrentValue(ObservedMouseOverProperty, frameworkElement.IsMouseOver);
        }
    }
}
