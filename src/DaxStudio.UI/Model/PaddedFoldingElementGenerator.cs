using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Rendering;

namespace DaxStudio.UI.Model
{
    internal sealed class PaddedFoldingElementGenerator : VisualLineElementGenerator
    {
        private const double LeftGap = 4.0;
        private readonly FoldingManager _foldingManager;

        public PaddedFoldingElementGenerator(FoldingManager foldingManager)
        {
            _foldingManager = foldingManager ?? throw new ArgumentNullException(nameof(foldingManager));
        }

        public override int GetFirstInterestedOffset(int startOffset)
        {
            return _foldingManager.GetNextFoldedFoldingStart(startOffset);
        }

        public override VisualLineElement ConstructElement(int offset)
        {
            int foldedUntil = -1;
            FoldingSection match = null;
            foreach (var folding in _foldingManager.GetFoldingsContaining(offset))
            {
                if (folding.IsFolded && folding.EndOffset > foldedUntil)
                {
                    foldedUntil = folding.EndOffset;
                    match = folding;
                }
            }

            if (match == null || foldedUntil <= offset) return null;

            bool foundOverlap;
            do
            {
                foundOverlap = false;
                foreach (var folding in _foldingManager.GetFoldingsContaining(foldedUntil))
                {
                    if (folding.IsFolded && folding.EndOffset > foldedUntil)
                    {
                        foldedUntil = folding.EndOffset;
                        foundOverlap = true;
                    }
                }
            } while (foundOverlap);

            var title = string.IsNullOrEmpty(match.Title) ? "..." : match.Title;
            if (!char.IsWhiteSpace(title[0])) title = " " + title;
            return new PaddedFoldingElement(match, title, foldedUntil - offset);
        }

        private sealed class PaddedFoldingElement : FormattedTextElement
        {
            private readonly FoldingSection _folding;

            public PaddedFoldingElement(
                FoldingSection folding,
                string title,
                int documentLength)
                : base(title, documentLength)
            {
                _folding = folding;
            }

            public override TextRun CreateTextRun(
                int startVisualColumn,
                ITextRunConstructionContext context)
            {
                TextRunProperties.SetForegroundBrush(FoldingElementGenerator.TextBrush);

                // Initialize the formatted text before creating our custom drawing run.
                var initializedRun = base.CreateTextRun(startVisualColumn, context);
                return new PaddedFoldingTextRun(this, initializedRun.Properties);
            }

            protected override void OnMouseDown(MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
                {
                    _folding.IsFolded = false;
                    e.Handled = true;
                    return;
                }

                base.OnMouseDown(e);
            }
        }

        private sealed class PaddedFoldingTextRun : FormattedTextRun
        {
            public PaddedFoldingTextRun(
                FormattedTextElement element,
                TextRunProperties properties)
                : base(element, properties)
            {
            }

            public override void Draw(
                DrawingContext drawingContext,
                Point origin,
                bool rightToLeft,
                bool sideways)
            {
                var metrics = Format(double.PositiveInfinity);
                var borderWidth = Math.Max(metrics.Width - LeftGap, 0);
                var border = new Rect(
                    origin.X + LeftGap,
                    origin.Y - metrics.Baseline,
                    borderWidth,
                    metrics.Height);

                drawingContext.DrawRectangle(
                    null,
                    new Pen(FoldingElementGenerator.TextBrush, 1),
                    border);
                base.Draw(drawingContext, origin, rightToLeft, sideways);
            }
        }
    }
}
