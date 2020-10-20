//
// Code copied from https://stackoverflow.com/questions/11149907/showing-invalid-xml-syntax-with-avalonedit
//

using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DAXEditorControl.Renderers
{
    public class TextMarkerService : IBackgroundRenderer, IVisualLineTransformer
    {
        private readonly TextEditor textEditor;
        private readonly TextSegmentCollection<TextMarker> markers;

        internal sealed class TextMarker : TextSegment
        {
            internal TextMarker(int startOffset, int length)
            {
                StartOffset = startOffset;
                Length = length;
            }

            public Color? BackgroundColor { get; set; }
            public Color MarkerColor { get; set; }
            public string ToolTip { get; set; }
        }

        internal TextMarkerService(TextEditor textEditor)
        {
            this.textEditor = textEditor;
            markers = new TextSegmentCollection<TextMarker>(textEditor.Document);
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            // validate parameters
            if (textView == null) throw new ArgumentNullException(nameof(textView));
            if (drawingContext == null) throw new ArgumentNullException(nameof(drawingContext));

            if (markers != null && textView.VisualLinesValid)
            {
                var visualLines = textView.VisualLines;
                if (visualLines.Count == 0)
                {
                    return;
                }
                int viewStart = visualLines.First().FirstDocumentLine.Offset;
                int viewEnd = visualLines.Last().LastDocumentLine.EndOffset;
                foreach (TextMarker marker in markers.FindOverlappingSegments(viewStart, viewEnd - viewStart))
                {
                    if (marker.BackgroundColor != null)
                    {
                        var geoBuilder = new BackgroundGeometryBuilder { AlignToWholePixels = true, CornerRadius = 3 };
                        geoBuilder.AddSegment(textView, marker);
                        Geometry geometry = geoBuilder.CreateGeometry();
                        if (geometry != null)
                        {
                            Color color = marker.BackgroundColor.Value;
                            var brush = new SolidColorBrush(color);
                            brush.Freeze();
                            drawingContext.DrawGeometry(brush, null, geometry);
                        }
                    }
                    foreach (Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker))
                    {
                        Point startPoint = r.BottomLeft;
                        Point endPoint = r.BottomRight;

                        double yOffset = 1;
                        startPoint.Offset(0, yOffset);
                        endPoint.Offset(0, yOffset);

                        var usedPen = new Pen(new SolidColorBrush(marker.MarkerColor), 1);
                        usedPen.Freeze();
                        const double offset = 2.5;

                        int count = Math.Max((int)((endPoint.X - startPoint.X) / offset) + 1, 4);

                        var geometry = new StreamGeometry();

                        using (StreamGeometryContext ctx = geometry.Open())
                        {
                            ctx.BeginFigure(startPoint, false, false);
                            ctx.PolyLineTo(CreatePoints(startPoint, offset, count).ToArray(), true, false);
                        }

                        geometry.Freeze();

                        drawingContext.DrawGeometry(Brushes.Transparent, usedPen, geometry);
                        break;
                    }
                }
            }
        }

        public KnownLayer Layer
        {
            get { return KnownLayer.Selection; }
        }

        public void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements)
        { }

        private static IEnumerable<Point> CreatePoints(Point start, double offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new Point(start.X + (i * offset), start.Y - ((i + 1) % 2 == 0 ? offset : 0));
            }
        }

        public void Clear()
        {
            foreach (TextMarker m in markers)
            {
                Remove(m);
            }
        }

        private void Remove(TextMarker marker)
        {
            if (markers.Remove(marker))
            {
                Redraw(marker);
            }
        }

        private void Redraw(ISegment segment)
        {
            textEditor.TextArea.TextView.Redraw(segment);
        }

        public void Create(int offset, int length, string message)
        {
            var m = new TextMarker(offset, length);
            markers.Add(m);
            m.MarkerColor = Colors.Red;
            m.ToolTip = message;
            Redraw(m);
        }

        internal IEnumerable<TextMarker> GetMarkersAtOffset(int offset)
        {
            return markers == null ? Enumerable.Empty<TextMarker>() : markers.FindSegmentsContaining(offset);
        }
    }
}
