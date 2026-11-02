using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Dax
{
    public class DAXCharStream : ICharStream
    {
        private ICharStream stream;
        private bool convertFromNonUS;

        public DAXCharStream(string dax) : this(new AntlrInputStream(dax), false)
        {

        }

        public DAXCharStream(string dax, bool convertFromNonUs) : this(new AntlrInputStream(dax), convertFromNonUs)
        {

        }

        /// <summary>
        /// Constructs a new CaseChangingCharStream wrapping the given <paramref name="stream"/> forcing
        /// all characters to upper case or lower case.
        /// </summary>
        /// <param name="stream">The stream to wrap.</param>
        /// <param name="upper">If true force each symbol to upper case, otherwise force to lower.</param>
        public DAXCharStream(ICharStream stream, bool convertFromNonUs)
        {
            this.stream = stream;
            this.convertFromNonUS = convertFromNonUs;
        }

        public int Index => stream.Index;

        public int Size => stream.Size;

        public string SourceName => stream.SourceName;

        public void Consume() => stream.Consume();

        [return: NotNull]
        public string GetText(Interval interval) => stream.GetText(interval);

        public int LA(int i)
        {
            int c = stream.LA(i);

            if (c <= 0)
            {
                return c;
            }

            char o = (char)c;

            if (convertFromNonUS)
            {
                if (o == ';') return (int)',';
                if (o == ',') return (int)'.';
            }
            return (int)char.ToUpperInvariant(o);
        }

        public int Mark() => stream.Mark();

        public void Release(int marker) => stream.Release(marker);

        public void Seek(int index) => stream.Seek(index);
    }
}
