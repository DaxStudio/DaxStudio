using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using ICSharpCode.AvalonEdit.Rendering;

namespace DAXEditorControl.Renderers
{
    /// <summary>
    /// Adds extra vertical spacing to each line, split evenly above and below the text.
    /// It injects a zero-width embedded element at the start of every visual line whose
    /// reported height and baseline force the line taller while keeping the text vertically
    /// centred within the added space. This is the only way to adjust line spacing in
    /// AvalonEdit 6.3.x without forking, as there is no public LineTransform / line-height option.
    /// </summary>
    public class LineSpacingElementGenerator : VisualLineElementGenerator
    {
        private double _extraSpacing;
        private bool _done;

        public LineSpacingElementGenerator(double extraSpacing)
        {
            _extraSpacing = extraSpacing;
        }

        /// <summary>Total extra spacing (in device-independent pixels) added to each line,
        /// distributed half above and half below the text.</summary>
        public double ExtraSpacing
        {
            get => _extraSpacing;
            set => _extraSpacing = value;
        }

        public override void StartGeneration(ITextRunConstructionContext context)
        {
            base.StartGeneration(context);
            // StartGeneration is called once per visual line, so reset the guard here.
            _done = false;
        }

        public override int GetFirstInterestedOffset(int startOffset)
        {
            if (_extraSpacing <= 0) return -1;
            var lineStart = CurrentContext.VisualLine.FirstDocumentLine.Offset;
            // Only insert once, at the very start of the line. Returning -1 after we've
            // inserted avoids an infinite loop caused by the zero-length element.
            if (!_done && startOffset <= lineStart) return lineStart;
            return -1;
        }

        public override VisualLineElement ConstructElement(int offset)
        {
            _done = true;
            return new LineSpacingVisualLineElement(_extraSpacing);
        }

        /// <summary>
        /// A zero-width visual line element that reserves extra vertical space and reports a
        /// baseline shifted by half the extra spacing, so the surrounding text is centred.
        /// </summary>
        private sealed class LineSpacingVisualLineElement : VisualLineElement
        {
            private readonly double _extraSpacing;

            public LineSpacingVisualLineElement(double extraSpacing)
                : base(1, 0)
            {
                _extraSpacing = extraSpacing;
            }

            public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
            {
                var height = context.TextView.DefaultLineHeight + _extraSpacing;
                var baseline = context.TextView.DefaultBaseline + (_extraSpacing / 2.0);
                return new LineSpacingRun(height, baseline, TextRunProperties);
            }
        }

        /// <summary>
        /// An invisible, zero-width embedded object whose metrics control the line height and
        /// baseline. Width is 0 so it consumes no horizontal space; it draws nothing.
        /// </summary>
        private sealed class LineSpacingRun : TextEmbeddedObject
        {
            private readonly double _height;
            private readonly double _baseline;
            private readonly TextRunProperties _properties;

            public LineSpacingRun(double height, double baseline, TextRunProperties properties)
            {
                _height = height;
                _baseline = baseline;
                _properties = properties;
            }

            public override LineBreakCondition BreakBefore => LineBreakCondition.BreakDesired;
            public override LineBreakCondition BreakAfter => LineBreakCondition.BreakDesired;
            public override bool HasFixedSize => true;
            public override CharacterBufferReference CharacterBufferReference => new CharacterBufferReference();
            public override int Length => 1;
            public override TextRunProperties Properties => _properties;

            public override TextEmbeddedObjectMetrics Format(double remainingParagraphWidth)
            {
                return new TextEmbeddedObjectMetrics(0, _height, _baseline);
            }

            public override Rect ComputeBoundingBox(bool rightToLeft, bool sideways)
            {
                return new Rect(0, 0, 0, _height);
            }

            public override void Draw(DrawingContext drawingContext, Point origin, bool rightToLeft, bool sideways)
            {
                // Nothing to draw - this element only affects line metrics.
            }
        }
    }
}
