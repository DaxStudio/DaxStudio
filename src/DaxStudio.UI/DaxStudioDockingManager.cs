using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Xceed.Wpf.AvalonDock;
using Xceed.Wpf.AvalonDock.Layout;

namespace DaxStudio.UI
{
    class DaxStudioDockingManager:DockingManager
    {
        
        public DaxStudioDockingManager()
        {
            this.DocumentClosing += OnDocumentClosing;
        }

        private void OnDocumentClosing(object sender, DocumentClosingEventArgs documentClosingEventArgs)
        {
            //documentClosingEventArgs.Document.Content
            Debug.WriteLine("Closing Tab");   
        }

        
    }
}
