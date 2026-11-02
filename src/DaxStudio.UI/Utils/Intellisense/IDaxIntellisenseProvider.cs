using ADOTabular;
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

        ADOTabularModel Model { get; }
        ADOTabularDynamicManagementViewCollection DMVs { get; }
        ADOTabularFunctionGroupCollection FunctionGroups { get; }

        // Copies the metadata that is normally populated by the *Loaded events. This is used when the
        // provider implementation is swapped at runtime (when the preview code completion option is
        // toggled) so the new provider does not have to wait for a reconnect to become usable.
        void SetCachedMetadata(ADOTabularModel model, ADOTabularDynamicManagementViewCollection dmvs, ADOTabularFunctionGroupCollection functionGroups);
    }
}
