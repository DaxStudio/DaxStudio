using System;
using System.Windows;
using System.Windows.Input;

namespace DaxStudio.UI.AttachedProperties
{
    public static class KeyboardFocusWithinObserver
    {
        public static readonly DependencyProperty ObserveProperty = DependencyProperty.RegisterAttached(
            "Observe",
            typeof(bool),
            typeof(KeyboardFocusWithinObserver),
            new FrameworkPropertyMetadata(OnObserveChanged));

        public static readonly DependencyProperty ObservedIsKeyboardFocusWithinProperty = DependencyProperty.RegisterAttached(
            "ObservedKeyboardFocusWithin",
            typeof(bool),
            typeof(KeyboardFocusWithinObserver));


        public static bool GetObserve(FrameworkElement frameworkElement)
        {
            return (bool)frameworkElement.GetValue(ObserveProperty);
        }

        public static void SetObserve(FrameworkElement frameworkElement, bool observe)
        {
            frameworkElement.SetValue(ObserveProperty, observe);
        }

        public static double GetObservedKeyboardFocusWithin(FrameworkElement frameworkElement)
        {
            return (double)frameworkElement.GetValue(ObservedIsKeyboardFocusWithinProperty);
        }

        public static void SetObservedKeyboardFocusWithin(FrameworkElement frameworkElement, double observedKeyboardFocusWithin)
        {
            frameworkElement.SetValue(ObservedIsKeyboardFocusWithinProperty, observedKeyboardFocusWithin);
        }

        private static void OnObserveChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var frameworkElement = (FrameworkElement)dependencyObject;
            if ((bool)e.NewValue)
            {
                frameworkElement.IsKeyboardFocusWithinChanged += OnFrameworkElementIsKeyboardFocusWithinChanged;
                
                UpdateObservedIsKeyboardFocusWithinForFrameworkElement(frameworkElement);
            }
            else
            {
                frameworkElement.IsKeyboardFocusWithinChanged -= OnFrameworkElementIsKeyboardFocusWithinChanged;
            }
        }

        private static void OnFrameworkElementIsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateObservedIsKeyboardFocusWithinForFrameworkElement((FrameworkElement)sender);
        }

        //private static void OnFrameworkElementIsKeyboardFocusWithinChanged(object sender, KeyboardFocusChangedEventArgs e)
        //{
        //    UpdateObservedIsKeyboardFocusWithinForFrameworkElement((FrameworkElement)sender);
        //}

        private static void UpdateObservedIsKeyboardFocusWithinForFrameworkElement(FrameworkElement frameworkElement)
        {
            frameworkElement.SetCurrentValue(ObservedIsKeyboardFocusWithinProperty, frameworkElement.IsKeyboardFocusWithin);
        }
    }
}
