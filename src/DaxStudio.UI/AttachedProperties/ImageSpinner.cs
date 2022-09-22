using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DaxStudio.UI.AttachedProperties
{
    public class ImageSpinner
    {
        private static readonly string SpinnerStoryBoardName = "ImageSpinnerStoryboard";
        private static void SpinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

            var element = d as Image;
            if (element != null)
            {
                if ((bool)e.NewValue == true)
                {
                    BeginSpin(element);
                }
                else
                {
                    StopSpin(element);
                    ResetRotation(element);
                }
            }
        }

        public static readonly DependencyProperty SpinProperty = DependencyProperty.RegisterAttached("Spin",
            typeof(bool),
            typeof(ImageSpinner),
            new PropertyMetadata(false, SpinChanged));

        public static void SetSpin(Image element, bool value)
        {

            element.SetValue(SpinProperty, value);
        }

        public static bool GetSpin(Image element)
        {

            return (bool)element.GetValue(SpinProperty);
        }


        public static void BeginSpin<T>(T control)
            where T : FrameworkElement
        {
            var transformGroup = control.RenderTransform as TransformGroup ?? new TransformGroup();

            var rotateTransform = transformGroup.Children.OfType<RotateTransform>().FirstOrDefault();

            if (rotateTransform != null)
            {
                rotateTransform.Angle = 0;
            }
            else
            {
                transformGroup.Children.Add(new RotateTransform(0.0));
                control.RenderTransform = transformGroup;
                control.RenderTransformOrigin = new Point(0.5, 0.5);
            }

            var storyboard = new Storyboard();

            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                AutoReverse = false,
                RepeatBehavior = RepeatBehavior.Forever,
                Duration = new Duration(TimeSpan.FromSeconds(1))
            };
            storyboard.Children.Add(animation);

            var propPath = "(0).(1)[0].(2)";
            //propPath = "RenderTransform.(RotateTransform.Angle)";
            Storyboard.SetTarget(animation, control);
            Storyboard.SetTargetProperty(animation,
                new PropertyPath(propPath, UIElement.RenderTransformProperty,
                    TransformGroup.ChildrenProperty, RotateTransform.AngleProperty));

            storyboard.Begin();
            control.Resources.Add(SpinnerStoryBoardName, storyboard);
        }

        public static void StopSpin<T>( T control)
    where T : FrameworkElement
        {
            var storyboard = control.Resources[SpinnerStoryBoardName] as Storyboard;

            if (storyboard == null) return;

            storyboard.Stop();

            control.Resources.Remove(SpinnerStoryBoardName);
        }

        public static void ResetRotation<T>(T control)
            where T : FrameworkElement
        {
            var transformGroup = control.RenderTransform as TransformGroup ?? new TransformGroup();

            var rotateTransform = transformGroup.Children.OfType<RotateTransform>().FirstOrDefault();

            if (rotateTransform != null)
            {
                rotateTransform.Angle = 0.0;
            }
            else
            {
                transformGroup.Children.Add(new RotateTransform(0.0));
                control.RenderTransform = transformGroup;
                control.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }
    }
}
