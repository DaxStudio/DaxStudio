using System;
using System.Reflection;
using System.Windows;
using Caliburn.Micro;
using DaxStudio.UI;

namespace DaxStudio.Standalone
{
    public class EntryPoint 
    {
        
        // All WPF applications should execute on a single-threaded apartment (STA) thread
        [STAThread]
        public static void Main()
        {
            // need to create application first
            var app = new Application();
            // then load Caliburn Micro bootstrapper
            var bootstrapper = new AppBootstrapper(Assembly.GetAssembly(typeof(DaxStudioHost)),true);
            
            app.Run();
        }
        
    }
}
