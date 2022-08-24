using DaxStudio.UI.Interfaces;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DaxStudio.UI.Controls
{
    public class ZoomableUserControl:UserControl
    {
        IZoomable _viewModel;
        public ZoomableUserControl()
        {
            this.PreviewMouseWheel += OnPreviewMouseWheel;
            this.DataContextChanged += OnDataContextChanged;

        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _viewModel = this.DataContext as IZoomable;
            Debug.Assert(_viewModel != null, $"The view model '{DataContext.GetType() }' does not support IZoomable");
            if (_viewModel == null) return;
            _viewModel.OnScaleChanged += ViewModel_OnScaleChanged;
        }

        private void ViewModel_OnScaleChanged(object sender, EventArgs args)
        {
            if (_viewModel == null) return;
            var scaleTransform = this.LayoutTransform as System.Windows.Media.ScaleTransform;
            if (scaleTransform == null)
            {
                this.LayoutTransform = new System.Windows.Media.ScaleTransform(_viewModel.Scale, _viewModel.Scale);
            }
            else
            {
                scaleTransform.ScaleX = _viewModel.Scale;
                scaleTransform.ScaleY = _viewModel.Scale;
            }
        }

        public void OnPreviewMouseWheel(object sender, MouseWheelEventArgs args)
        {
            Debug.Assert(_viewModel != null);
            if (_viewModel == null) return;
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                var factor = 1.0 + Math.Abs(args.Delta / 1200.0);
                var scale = args.Delta >= 0 ? factor : (1.0 / factor); // choose appropriate scaling factor
                _viewModel.Scale *= scale;
                args.Handled = true;
            }

        }

    }
}
