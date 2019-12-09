// This code was largely "borrowed" from http://edi.codeplex.com

namespace DAXEditorControl.BracketRenderer
{
  /// <summary>
  /// Describes a pair of matching brackets found by IBracketSearcher.
  /// </summary>
  public class BracketSearchResult
  {
    public int OpeningBracketOffset { get; private set; }

    public int OpeningBracketLength { get; private set; }

    public int ClosingBracketOffset { get; private set; }

    public int ClosingBracketLength { get; private set; }

    public BracketSearchResult(int openingBracketOffset, int openingBracketLength,
                               int closingBracketOffset, int closingBracketLength)
    {
      this.OpeningBracketOffset = openingBracketOffset;
      this.OpeningBracketLength = openingBracketLength;
      this.ClosingBracketOffset = closingBracketOffset;
      this.ClosingBracketLength = closingBracketLength;
    }
  }
}
