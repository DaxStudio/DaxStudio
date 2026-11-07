using System;

namespace DaxStudio.Parsers.StorageEngine
{
    /// <summary>The keywords of a grammar, which its lexer never reads as an identifier, in any case.</summary>
    internal sealed class KeywordSet
    {
        private readonly string[][] _keywordsByLength;

        public KeywordSet(params string[] keywords)
        {
            var max = 0;
            foreach (var k in keywords) max = Math.Max(max, k.Length);
            _keywordsByLength = new string[max + 1][];
            for (var length = 0; length <= max; length++)
            {
                _keywordsByLength[length] = Array.FindAll(keywords, k => k.Length == length);
            }
        }

        public bool Contains(ReadOnlySpan<char> word)
        {
            if (word.Length >= _keywordsByLength.Length) return false;
            foreach (var keyword in _keywordsByLength[word.Length])
            {
                if (word.Equals(keyword.AsSpan(), StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
