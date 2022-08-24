using AvalonDock;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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
