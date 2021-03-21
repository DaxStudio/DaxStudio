using DaxStudio.UI.ViewModels;
using System.Windows.Controls;

namespace DaxStudio.UI.Views
{
    /// <summary>
    /// Interaction logic for DocumentView.xaml
    /// </summary>
    public partial class DocumentView : UserControl
    {
        public DocumentView()
        {
            InitializeComponent();
        }

        private void ContextMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            ((DocumentViewModel)DataContext).EditorContextMenuOpening();
        }
    }
}
