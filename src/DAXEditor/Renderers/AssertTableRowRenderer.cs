using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace DAXEditorControl.Renderers
{
    /// <summary>
    /// Draws a faint, full-width tint behind Comment Script ASSERT TABLE continuation rows
    /// (the "-->>" prefixed lines) so the inline expected-results table stands out from the
    /// surrounding query text. The fill colour is read from the (unreferenced)
    /// "CsTableRowBackground" highlighting colour so that it follows the current light/dark theme.
    /// </summary>
    public class AssertTableRowRenderer : IBackgroundRenderer
    {
        // matches leading whitespace followed by the "-->>" continuation marker
        private static readonly Regex TableRowRegex =
            new Regex(@"^[ \t]*-->>", RegexOptions.Compiled);

        // corner radius (in DIPs) applied to the rounded blue row-group rectangle
        private const double CornerRadius = 3.0;

        private readonly TextEditor _editor;

        public AssertTableRowRenderer(TextEditor editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        }

        // draw underneath the text so the row glyphs remain visible on top
        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView == null || drawingContext == null) return;
            if (!textView.VisualLinesValid) return;

            var brush = GetRowBrush();
            if (brush == null) return;

            var document = _editor.Document;
            if (document == null) return;

            double fullWidth = textView.ActualWidth;

            // Coalesce runs of consecutive "-->>" rows into a single rectangle so there are no faint
            // seams between the per-line fills. Track the current run's top/bottom and flush it when a
            // non-matching line (or the end of the visible lines) is reached.
            bool haveRun = false;
            double runTop = 0, runBottom = 0;

            void Flush()
            {
                if (!haveRun) return;
                double width = Math.Max(fullWidth, 0);
                drawingContext.DrawRoundedRectangle(brush, null,
                    new Rect(0, runTop, width, runBottom - runTop), CornerRadius, CornerRadius);
                haveRun = false;
            }

            foreach (var visualLine in textView.VisualLines)
            {
                var documentLine = visualLine.FirstDocumentLine;
                var text = document.GetText(documentLine.Offset, documentLine.Length);

                if (!TableRowRegex.IsMatch(text))
                {
                    Flush();
                    continue;
                }

                var segment = new TextSegment { StartOffset = documentLine.Offset, EndOffset = documentLine.EndOffset };
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    if (!haveRun)
                    {
                        runTop = rect.Top;
                        runBottom = rect.Bottom;
                        haveRun = true;
                    }
                    else
                    {
                        if (rect.Top < runTop) runTop = rect.Top;
                        if (rect.Bottom > runBottom) runBottom = rect.Bottom;
                    }
                }
            }
            Flush();
        }

        private Brush GetRowBrush()
        {
            var definition = _editor.SyntaxHighlighting;
            var color = definition?.GetNamedColor("CsTableRowBackground");
            var brush = color?.Background?.GetBrush(null);
            return brush;
        }
    }
}
