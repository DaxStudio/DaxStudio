using System.Windows.Media;
using Caliburn.Micro;
namespace DaxStudio.UI.Interfaces
{
    public interface IToolWindow
    {
        string Title { get;  }
        string DefaultDockingPane { get; }
        bool CanCloseWindow { get; set; }
        bool CanHide { get; set; }
        int AutoHideMinHeight { get; set; }
        bool IsSelected { get; set; }
        //bool IsActive { get; set; }
        
        string ContentId { get; }
        ImageSource IconSource { get; }
    }
}
