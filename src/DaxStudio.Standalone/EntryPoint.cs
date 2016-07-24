using System;
using System.Reflection;
using System.Windows;
using Caliburn.Micro;
using DaxStudio.UI;
using Serilog;
using DaxStudio.UI.Utils;
namespace DaxStudio.Standalone
{
    public static class EntryPoint 
    {
        public static ILogger log;
        static EntryPoint()
        {
//            log = new LoggerConfiguration().ReadAppSettings().CreateLogger();
            
//            //log = new LoggerConfiguration().WriteTo.Loggly().CreateLogger();
//#if DEBUG
//            Serilog.Debugging.SelfLog.Out =  Console.Out;
//#endif
//            Log.Logger = log;
//            Log.Information("============ DaxStudio Startup =============");
//            //AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
            
        }

        
        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            Log.Warning("Class {0} Method {1} RequestingAssembly: {2} Name: {3}", "EntryPoint", "ResolveAssembly", args.RequestingAssembly, args.Name);
            System.Diagnostics.Debug.WriteLine(string.Format("ReqAss: {0}, Name{1}", args.RequestingAssembly, args.Name));
            if (args.Name.StartsWith("Microsoft.AnalysisServices")) return SsasAssemblyResolver.Instance.Resolve(args.Name);
            return null;
        }
        
        
        // All WPF applications should execute on a single-threaded apartment (STA) thread
        [STAThread]
        public static void Main()
        {
            try
            {
                // need to create application first
                var app = new Application();
                // then load Caliburn Micro bootstrapper
                var bootstrapper = new AppBootstrapper(Assembly.GetAssembly(typeof(DaxStudioHost)), true);


                log = new LoggerConfiguration().ReadAppSettings().CreateLogger();

                //log = new LoggerConfiguration().WriteTo.Loggly().CreateLogger();
#if DEBUG
                Serilog.Debugging.SelfLog.Out = Console.Out;
#endif
                Log.Logger = log;
                Log.Information("============ DaxStudio Startup =============");
                //SsasAssemblyResolver.Instance.BuildAssemblyCache();

                AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;


                app.Run();
            }
            catch (Exception ex)
            {
                Log.Error("Class: {0} Method: {1} Error: {2} Stack: {3}", "EntryPoint", "Main", ex.Message, ex.StackTrace);
#if DEBUG 
                MessageBox.Show(ex.Message);
#endif
            }
            finally
            {
                Log.Information("============ DaxStudio Shutdown =============");
            }
        }
        
    }
}
