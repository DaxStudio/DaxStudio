// This code was largely "borrowed" from http://edi.codeplex.com
namespace DAXEditorControl.BracketRenderer
{
  public class DefaultBracketSearcher : IBracketSearcher
  {
    public static readonly DefaultBracketSearcher DefaultInstance = new DefaultBracketSearcher();

    public BracketSearchResult SearchBracket(ICSharpCode.AvalonEdit.Document.ITextSource document, int offset)
    {
      return null;
    }
  }
}
