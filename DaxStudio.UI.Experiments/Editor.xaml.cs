using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DaxStudio.UI.Experiments
{
    /// <summary>
    /// Interaction logic for Editor.xaml
    /// </summary>
    public partial class Editor : Window
    {
        public Editor()
        {
            InitializeComponent();
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Dropped");
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("DragEnter");
        }

        private void TxtOnDrop(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("TB Drop");
        }

        private void TxtDragEnter(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("TB Drag Enter");
        }

        private void TxtDragOver(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("TB Drag Over");
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Drag Over");
        }

        private void OnDropPreview(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Drop Preview");
            //e.Handled = true;
            
        }

        private void OnWindowDrop(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Window Drop");
        }

    }
}
