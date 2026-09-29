using System.Collections.Generic;
using Antlr4.Runtime;
using DaxStudio.Parsers.Grammars.Generated;
using DaxStudio.Parsers.PreProcessor;

namespace DaxStudio.Parsers.Dax
{
    internal struct DaxTextChunk
    {
        public int StartOffset { get; }
        public int EndOffset { get; }
        public string Text { get; }

        public DaxTextChunk(int startOffset, int endOffset, string text)
        {
            StartOffset = startOffset;
            EndOffset = endOffset;
            Text = text ?? string.Empty;
        }
    }

    internal static class DaxStructuralChunker
    {
        public static IReadOnlyList<DaxTextChunk> GetChunks(string input)
        {
            input = input ?? string.Empty;
            var chunks = new List<DaxTextChunk>();

            foreach (var batch in AntlrPreProcessor.SplitOnGo(input))
            {
                AddBatchChunks(input, batch, chunks);
            }

            return chunks;
        }

        public static DaxTextChunk GetChunkAtCursor(string input, int cursorOffset)
        {
            input = input ?? string.Empty;
            if (cursorOffset < 0) cursorOffset = 0;
            if (cursorOffset > input.Length) cursorOffset = input.Length;

            var chunks = GetChunks(input);
            if (chunks.Count == 0)
                return new DaxTextChunk(0, cursorOffset, input.Substring(0, cursorOffset));

            var selected = chunks[0];
            foreach (var chunk in chunks)
            {
                if (chunk.StartOffset > cursorOffset) break;
                selected = chunk;
            }

            return new DaxTextChunk(
                selected.StartOffset,
                cursorOffset,
                input.Substring(selected.StartOffset, cursorOffset - selected.StartOffset));
        }

        private static void AddBatchChunks(string input, SourceTextSegment batch, List<DaxTextChunk> chunks)
        {
            var batchText = input.Substring(batch.StartOffset, batch.EndOffset - batch.StartOffset);
            var boundaries = GetDefineOffsets(batchText);
            var chunkStart = 0;

            foreach (var boundary in boundaries)
            {
                if (boundary > chunkStart)
                    AddChunk(input, batch.StartOffset + chunkStart, batch.StartOffset + boundary, chunks);
                chunkStart = boundary;
            }

            AddChunk(input, batch.StartOffset + chunkStart, batch.EndOffset, chunks);
        }

        private static IReadOnlyList<int> GetDefineOffsets(string input)
        {
            var offsets = new List<int>();
            ICharStream chars = new DAXCharStream(input);
            var lexer = new DAXLexer(chars);
            lexer.RemoveErrorListeners();

            foreach (var token in lexer.GetAllTokens())
            {
                if (token.Channel == TokenConstants.DefaultChannel && token.Type == DAXLexer.DEFINE)
                    offsets.Add(token.StartIndex);
            }

            return offsets;
        }

        private static void AddChunk(string input, int startOffset, int endOffset, List<DaxTextChunk> chunks)
        {
            if (endOffset < startOffset) return;
            chunks.Add(new DaxTextChunk(
                startOffset,
                endOffset,
                input.Substring(startOffset, endOffset - startOffset)));
        }
    }
}
