using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    public static class ZoomHelper
    {
        public static void PreviewMouseWheel(System.Windows.Controls.UserControl sender, System.Windows.Input.MouseWheelEventArgs args)
        {
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                var factor = 1.0 + System.Math.Abs(((double)args.Delta) / 1200.0);
                var scale = args.Delta >= 0 ? factor : (1.0 / factor); // choose appropriate scaling factor
                var scaler = sender.LayoutTransform as System.Windows.Media.ScaleTransform;
                if (scaler == null)
                {
                    sender.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
                }
                else
                {
                    scaler.ScaleX *= scale;
                    scaler.ScaleY *= scale;
                }
                args.Handled = true;
            }
        }
    }
}
