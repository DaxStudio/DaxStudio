
using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Allows producing foldings from a document based on indentation.
    /// </summary>
    public class IndentFoldingStrategy
    {
        private List<NewFolding> newFoldings = new List<NewFolding>();
        private ITextSourceVersion prevVersion;
        /// <summary>
        /// Creates a new BraceFoldingStrategy.
        /// </summary>
        public IndentFoldingStrategy()
        {
            
        }

        public int TabIndent { get; set; } = 4; 

        public void UpdateFoldings(FoldingManager manager, TextDocument document)
        {
            if (prevVersion != null && document.Version.CompareAge(prevVersion) == 0) return;

            prevVersion = document.Version;

            int firstErrorOffset;
            IEnumerable<NewFolding> newFoldings = CreateNewFoldings(document, out firstErrorOffset);
            manager.UpdateFoldings(newFoldings, firstErrorOffset);
        }

        /// <summary>
        /// Create <see cref="NewFolding"/>s for the specified document.
        /// </summary>
        public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
        {
            firstErrorOffset = -1;
            return CreateNewFoldings(document);
        }


        private struct Indent
        {
            public int Offset; 
            public int IndentDepth;

            public Indent(int offset, int indentDepth) : this()
            {
                this.Offset = offset;
                this.IndentDepth = indentDepth;
            }
        }

        // <summary>
        /// Create <see cref="NewFolding"/>s for the specified document.
        /// </summary>
        public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document)
        {
            //if (prevVersion != null && document.Version.CompareAge(prevVersion) == 0) return newFoldings;
            
            //prevVersion = document.Version;

            newFoldings = new List<NewFolding>();

            var startOffsets = new Stack<Indent>();
            int lastIndentOffset = 0;
            int lineIndent = 0;
            
            foreach (DocumentLine line in document.Lines)
            {
                lineIndent = 0;
                for (int i = line.Offset; i < line.EndOffset; i++)
                {
                    char c = document.GetCharAt(i);
                    if (c == '\t')
                    {
                        lineIndent += TabIndent;
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        lineIndent++;
                    }
                    else
                    {
                        break;
                    }
                }
                var isWhitespaceLine = lineIndent + line.DelimiterLength == line.TotalLength;

                if (lineIndent > lastIndentOffset)
                {
                    startOffsets.Push(new Indent( line.Offset - (line.Offset> line.DelimiterLength ?line.DelimiterLength:0), lineIndent));                    
                }
                else if (lineIndent < lastIndentOffset 
                    && !isWhitespaceLine
                    && startOffsets.Count > 0 )
                {
                    var startOffset = startOffsets.Pop();
                    int endOffset = line.Offset - line.DelimiterLength ;
                    if (endOffset > document.TextLength) endOffset = document.TextLength;
                        
                    newFoldings.Add(new NewFolding(startOffset.Offset, endOffset));
                    while (startOffset.IndentDepth > lineIndent && startOffsets.Count > 0 && startOffsets.Peek().IndentDepth > lineIndent)
                    {
                        startOffset = startOffsets.Pop();
                        newFoldings.Add(new NewFolding(startOffset.Offset, endOffset));
                    }
                }

                // only set the lastIndentOffset if the current line is not whitespace
                if (!isWhitespaceLine)
                {
                    lastIndentOffset = lineIndent;
                }
            }

            while (startOffsets.Count > 0)
            {
                var startOffset = startOffsets.Pop();
                var endOffset = document.Lines[document.Lines.Count - 1].EndOffset;
                newFoldings.Add(new NewFolding(startOffset.Offset, endOffset));
            }

            newFoldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
            return newFoldings;
        }

    }
}
