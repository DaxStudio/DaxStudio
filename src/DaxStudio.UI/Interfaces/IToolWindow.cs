using System.Windows.Media;
using Caliburn.Micro;
namespace DaxStudio.UI.Interfaces
{
    public interface IToolWindow
    {
        string Title { get;  }
        string DefaultDockingPane { get; }
        bool CanCloseWindow { get; set; }
        bool CanHide { get;  }
        int AutoHideMinHeight { get; set; }
        bool IsSelected { get; set; }
        //bool IsActive { get; set; }
        //bool IsVisible { get; set; }
        string ContentId { get; }
        //ImageSource IconSource { get; }
    }
}
