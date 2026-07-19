using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace DAXEditorControl.Renderers
{
    /// <summary>
    /// Draws a rounded, padded "chip" behind the command keyword of a Comment Script
    /// directive line (eg. the CONNECT in "--> CONNECT PBIX ..."). AvalonEdit's syntax
    /// highlighting can only paint a flat rectangle tight around the glyphs, so this
    /// background renderer is used instead to get padding and rounded corners.
    /// The fill colour is read from the (unreferenced) "CsChipBackground" highlighting
    /// colour so that it follows the current light/dark theme.
    /// </summary>
    public class CommentScriptChipRenderer : IBackgroundRenderer
    {
        // matches leading whitespace, the directive marker (--> or -->>), whitespace,
        // then the command keyword. Case-insensitive to be forgiving.
        private static readonly Regex DirectiveRegex =
            new Regex(@"^[ \t]*--?>>?[ \t]*([A-Za-z]+)", RegexOptions.Compiled);

        // The set of command verbs that get a chip. Keep in sync with the CsCommand
        // keyword list in Resources\DAX.xshd.
        private static readonly HashSet<string> CommandKeywords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CONNECT", "USE", "OPEN", "PARAMETER", "SET", "OUTPUT", "TRACE",
                "CLEARCACHE", "SAVEAS", "METRICS", "SHOW", "ASSERT", "TEST", "RESULTS", "GO"
            };

        private const double HorizontalPadding = 3.0;
        private const double VerticalPadding = 1.0;
        private const double CornerRadius = 3.0;

        private readonly TextEditor _editor;

        public CommentScriptChipRenderer(TextEditor editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        }

        // draw underneath the text so the keyword glyphs remain visible on top
        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView == null || drawingContext == null) return;
            if (!textView.VisualLinesValid) return;

            var brush = GetChipBrush();
            if (brush == null) return;

            var document = _editor.Document;
            if (document == null) return;

            foreach (var visualLine in textView.VisualLines)
            {
                var documentLine = visualLine.FirstDocumentLine;
                var text = document.GetText(documentLine.Offset, documentLine.Length);

                var match = DirectiveRegex.Match(text);
                if (!match.Success) continue;

                var keywordGroup = match.Groups[1];
                if (!CommandKeywords.Contains(keywordGroup.Value)) continue;

                var startOffset = documentLine.Offset + keywordGroup.Index;
                var endOffset = startOffset + keywordGroup.Length;

                var segment = new TextSegment { StartOffset = startOffset, EndOffset = endOffset };
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    var chip = new Rect(
                        rect.Left - HorizontalPadding,
                        rect.Top - VerticalPadding,
                        rect.Width + (HorizontalPadding * 2),
                        rect.Height + (VerticalPadding * 2));
                    drawingContext.DrawRoundedRectangle(brush, null, chip, CornerRadius, CornerRadius);
                }
            }
        }

        private Brush GetChipBrush()
        {
            var definition = _editor.SyntaxHighlighting;
            var color = definition?.GetNamedColor("CsChipBackground");
            var brush = color?.Background?.GetBrush(null);
            return brush;
        }
    }
}
