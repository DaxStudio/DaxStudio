using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace DAXEditorControl.Renderers
{
    public sealed class CommentScriptLineRenderer : IBackgroundRenderer
    {
        private readonly TextEditor _editor;

        public CommentScriptLineRenderer(TextEditor editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        }

        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView == null || drawingContext == null || !textView.VisualLinesValid) return;

            var document = _editor.Document;
            if (document == null) return;

            foreach (var visualLine in textView.VisualLines)
            {
                var documentLine = visualLine.FirstDocumentLine;
                var text = document.GetText(documentLine.Offset, documentLine.Length);
                if (!CommentScriptCommandStyle.TryClassify(text, out var style)) continue;

                var brush = GetBackgroundBrush(style.RowBackgroundColorName);
                if (brush == null) continue;

                var segment = new TextSegment
                {
                    StartOffset = documentLine.Offset,
                    EndOffset = documentLine.EndOffset
                };
                var rects = new List<Rect>();
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    rects.Add(rect);
                    drawingContext.DrawRectangle(
                        brush,
                        null,
                        new Rect(0, rect.Top, Math.Max(textView.ActualWidth, 0), rect.Height));
                }

                if (style.IsBatchSeparator && rects.Count > 0)
                    DrawBatchSeparators(textView, drawingContext, style.CommandColorName, rects);
            }
        }

        private void DrawBatchSeparators(
            TextView textView,
            DrawingContext drawingContext,
            string colorName,
            IReadOnlyList<Rect> rects)
        {
            var brush = GetForegroundBrush(colorName);
            if (brush == null) return;

            const double thickness = 1.0;
            var pen = new Pen(brush, thickness);
            if (pen.CanFreeze) pen.Freeze();

            var width = Math.Max(textView.ActualWidth, 0);
            var top = rects[0].Top + (thickness / 2);
            var bottom = rects[rects.Count - 1].Bottom - (thickness / 2);
            drawingContext.DrawLine(pen, new Point(0, top), new Point(width, top));
            drawingContext.DrawLine(pen, new Point(0, bottom), new Point(width, bottom));
        }

        private Brush GetBackgroundBrush(string colorName)
        {
            var color = _editor.SyntaxHighlighting?.GetNamedColor(colorName);
            return color?.Background?.GetBrush(null);
        }

        private Brush GetForegroundBrush(string colorName)
        {
            var color = _editor.SyntaxHighlighting?.GetNamedColor(colorName);
            return color?.Foreground?.GetBrush(null);
        }
    }
}
