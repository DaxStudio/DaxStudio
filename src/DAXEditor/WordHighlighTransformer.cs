﻿using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System.Collections.Generic;
using System.Windows.Media;

namespace DAXEditorControl
{
    sealed class WordHighlighTransformer : DocumentColorizingTransformer
    {
        private HighlightDelegate _highlightFunc;
        private Brush _highlightColour;
        public WordHighlighTransformer(HighlightDelegate func, Brush highlightColour)
        {
            _highlightFunc = func;
            _highlightColour = highlightColour;
        }

        public int FindSelectionLength { get; internal set; }
        public int FindSelectionOffset { get; internal set; }
        private int FindSelectionEndOffset => FindSelectionOffset + FindSelectionLength;
        public HighlightDelegate HightlightFunction { get => _highlightFunc; internal set => _highlightFunc = value; }

        /*
protected override void Colorize(ITextRunConstructionContext context)
{
//base.Colorize(context);
foreach (var line in context.Document.Lines)
{
ColorizeText(line, context.Document.GetText(line));

}
}
*/
        protected override void ColorizeLine(DocumentLine line)
            {
                
                if (CurrentContext == null) return;
                
                string text = CurrentContext.Document.GetText(line);
                ColorizeText(line, text);
            }

            private void ColorizeText(DocumentLine line, string text)
            {
                int lineStartOffset = line.Offset;
                if (_highlightFunc == null) return; // if the highlight function is not set exit here
                List<HighlightPosition> positions = _highlightFunc(text, line.Offset, line.EndOffset);
                if (positions == null) return;

                foreach (HighlightPosition pos in positions)
                {
                    var startOffset = lineStartOffset + pos.Index;
                    var endOffset = lineStartOffset + pos.Index + pos.Length;

                    if ((startOffset < FindSelectionOffset 
                        || endOffset > FindSelectionEndOffset )
                        && FindSelectionLength > 0 ) continue; // skip positions not in the current selection

                base.ChangeLinePart(
                        startOffset, endOffset,
                        (VisualLineElement element) =>
                        {
                            element.TextRunProperties.SetBackgroundBrush(_highlightColour);
                        });

                }
            }


    }
}
