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

        private void daxEditor_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("DragEnter Event");
        }
    }
}
