using DaxStudio.UI.Interfaces;
using Serilog;
using System;
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
            // Detach from the previous DataContext (if any) so we don't leak handlers
            if (_viewModel != null)
            {
                _viewModel.OnScaleChanged -= ViewModel_OnScaleChanged;
                _viewModel = null;
            }

            _viewModel = e.NewValue as IZoomable;
            if (_viewModel == null)
            {
                if (e.NewValue != null)
                {
                    // Soft check: log instead of asserting so that view models which
                    // legitimately don't need zooming (or which lost IZoomable during a
                    // refactor) don't bring down the app via a Debug.Assert dialog.
                    Log.Warning("{class} {method} The view model '{viewModelType}' bound to {controlType} does not implement IZoomable; zoom support disabled for this view.",
                        nameof(ZoomableUserControl), nameof(OnDataContextChanged),
                        e.NewValue.GetType().FullName, this.GetType().FullName);
                }
                return;
            }
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
            if (_viewModel == null) return;
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                var factor = args.Delta / 1200.0;
                _viewModel.Scale += factor;
                args.Handled = true;
            }

        }

    }
}
