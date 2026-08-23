using DAXEditorControl;

namespace DaxStudio.UI.Utils.Intellisense
{
    /// <summary>
    /// Common surface used by the DocumentViewModel/MeasureExpressionEditorViewModel to interact with
    /// an intellisense provider regardless of which implementation (regex-based or ANTLR-based) is active.
    /// </summary>
    public interface IDaxIntellisenseProvider : IIntellisenseProvider
    {
        IEditor Editor { get; set; }
        void CloseCompletionWindow();
    }
}
