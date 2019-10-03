using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DAXEditorControl
{
    class WordHighlighTransformer : DocumentColorizingTransformer
    {
        private HighlightDelegate _highlightFunc;
        private Brush _highlightColour;
        public WordHighlighTransformer(HighlightDelegate func, Brush highlightColour)
        {
            _highlightFunc = func;
            _highlightColour = highlightColour;
        }
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
                    
                    base.ChangeLinePart(
                        lineStartOffset + pos.Index, // startOffset
                        lineStartOffset + pos.Index + pos.Length, // endOffset
                        (VisualLineElement element) =>
                        {
                            element.TextRunProperties.SetBackgroundBrush(_highlightColour);
                        });

                }
            }


    }
}
