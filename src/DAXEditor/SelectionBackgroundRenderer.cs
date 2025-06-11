using ICSharpCode.AvalonEdit.Rendering;
using System.Linq;
using System.Windows.Media;

namespace DAXEditorControl
{
    public class SelectionBackgroundRenderer : IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Background;

        public int StartOffset { get; set; }
        public int Length { get; set; }
        public int EndOffset => StartOffset + Length;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
             DrawImpl(textView, drawingContext);

        }

        private void DrawImpl(TextView textView, DrawingContext drawingContext)
        {

            var brush = (Brush)System.Windows.Application.Current.FindResource("Theme.Brush.FindSelection.Back");
            if (brush == null) return; // exit if no brush found

            ICSharpCode.AvalonEdit.Document.TextSegment seg = new ICSharpCode.AvalonEdit.Document.TextSegment();
            seg.StartOffset = StartOffset;
            seg.EndOffset = EndOffset;

            var rcArray = BackgroundGeometryBuilder.GetRectsForSegment(textView, seg, true);
            foreach (var rc in rcArray)
            {
                drawingContext.DrawRectangle(brush, null,
                    new System.Windows.Rect(rc.Left, rc.Top, rc.Width, rc.Height));
            }

        }
    }
}
