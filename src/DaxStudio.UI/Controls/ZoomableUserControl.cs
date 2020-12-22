using DaxStudio.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DaxStudio.UI.Controls
{
    public class ZoomableUserControl:UserControl
    {
        IZoomable viewModel;
        public ZoomableUserControl()
        {
            this.PreviewMouseWheel += OnPreviewMouseWheel;
            this.DataContextChanged += OnDataContextChanged;

        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            viewModel = this.DataContext as IZoomable;
            Debug.Assert(viewModel != null, $"The view model '{DataContext.GetType()}' does not support IZoomable");
            if (viewModel == null) return;
            viewModel.OnScaleChanged += ViewModel_OnScaleChanged;
        }

        private void ViewModel_OnScaleChanged(object sender, EventArgs args)
        {
            if (viewModel == null) return;
            var scaler = this.LayoutTransform as System.Windows.Media.ScaleTransform;
            if (scaler == null)
            {
                this.LayoutTransform = new System.Windows.Media.ScaleTransform(viewModel.Scale, viewModel.Scale);
            }
            else
            {
                scaler.ScaleX = viewModel.Scale;
                scaler.ScaleY = viewModel.Scale;
            }
        }

        public void OnPreviewMouseWheel(object sender, MouseWheelEventArgs args)
        {
            Debug.Assert(viewModel != null);
            if (viewModel == null) return;
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                var factor = 1.0 + System.Math.Abs(((double)args.Delta) / 1200.0);
                var scale = args.Delta >= 0 ? factor : (1.0 / factor); // choose appropriate scaling factor
                viewModel.Scale *= scale;
                args.Handled = true;
            }

        }

    }
}
