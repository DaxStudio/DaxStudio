using ICSharpCode.AvalonEdit.Rendering;
using System.Linq;
using System.Windows.Media;

namespace DaxStudio.UI.Utils
{
    public class SelectionBackgroundRenderer : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Background;

        public int StartOffset { get; set; }
        public int Length { get; set; }
        public int EndOffset => StartOffset + Length;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            foreach (var visualLine in textView.VisualLines)
            {
                if (StartOffset > (visualLine.StartOffset + visualLine.VisualLength)) continue;
                if (EndOffset < (visualLine.StartOffset + visualLine.VisualLength)) continue;

                var rc = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, visualLine, 0, 1000).First();

                var brush = (Brush)System.Windows.Application.Current.FindResource("Theme.Brush.FindSelection.Back");
                if (brush == null) return;

                drawingContext.DrawRectangle(brush, null,
                    new System.Windows.Rect(0, rc.Top, textView.ActualWidth, rc.Height));
            }
        }
    }
}
