
using System.Collections.Generic;
using System.Linq;
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
            newFoldings = new List<NewFolding>();

            var startOffsets = new Stack<Indent>();
            int previousIndent = 0;
            int currentIndent = 0;
            int endOfPreviousLine = document.Lines[0].EndOffset; 
                     

            foreach (DocumentLine line in document.Lines)
            {

                currentIndent = GetIndent(document, line);

                var isWhitespaceLine = currentIndent + line.DelimiterLength == line.TotalLength;
                if (isWhitespaceLine) { continue; }

                if (currentIndent > previousIndent)
                {
                    // if the indent is deeper push the end of the previous line as the folding start 
                    startOffsets.Push(new Indent(endOfPreviousLine, previousIndent));
                }
                else if (currentIndent < previousIndent
                    && startOffsets.Any())
                {
                    var startOffset = startOffsets.Peek();

                    // get the end of the previous line
                    int endOffset = line.Offset - line.DelimiterLength;

                    while (startOffsets.Any() 
                        && startOffsets.Peek().IndentDepth >= currentIndent)
                    {
                        startOffset = startOffsets.Pop();
                        newFoldings.Add(new NewFolding(startOffset.Offset, endOffset));
                    }
                }

                previousIndent = currentIndent;
                endOfPreviousLine = line.EndOffset;

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

        private int GetIndent(TextDocument document, DocumentLine line)
        {
            int currentIndent = 0;
            for (int i = line.Offset; i < line.EndOffset; i++)
            {
                char c = document.GetCharAt(i);

                if (c == '\t')
                {
                    currentIndent += TabIndent; 
                    continue;
                }
                else if (char.IsWhiteSpace(c))
                {
                    currentIndent++;
                    continue;
                }
                break;
            }

            return currentIndent;
        }
    }
}
