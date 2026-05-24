using Caliburn.Micro;
namespace DaxStudio.Core.Interfaces
{
    public interface IToolWindow : IScreen
    {
        string Title { get; }
        string DefaultDockingPane { get; }
        bool CanCloseWindow { get; set; }
        bool CanHide { get; }
        int AutoHideMinHeight { get; set; }
        bool IsSelected { get; set; }
        string ContentId { get; }
    }
}