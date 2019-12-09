namespace DAXEditorControl.BracketRenderer
{
  /// <summary>
  /// Allows language specific search for matching brackets.
  /// </summary>
  public interface IBracketSearcher
  {
    /// <summary>
    /// Searches for a matching bracket from the given offset to the start of the document.
    /// </summary>
    /// <returns>A BracketSearchResult that contains the positions and lengths of the brackets. Return null if there is nothing to highlight.</returns>
    BracketSearchResult SearchBracket(ICSharpCode.AvalonEdit.Document.ITextSource document, int offset);
  }
}
