using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Common contract for the editor folding strategies (indentation based and structural /
    /// parser based) so <c>DocumentViewModel</c> can hold and swap between them.
    /// </summary>
    public interface IFoldingStrategy
    {
        void UpdateFoldings(FoldingManager manager, TextDocument document);
    }
}
