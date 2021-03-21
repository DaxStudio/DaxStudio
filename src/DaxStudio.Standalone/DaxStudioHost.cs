using System;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using System.Windows;
using DaxStudio.Common;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.Standalone
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IDaxStudioHost))]
    public class DaxStudioHost 
        : IDaxStudioHost
    {
        private readonly Application _app;

        //private UI.ViewModels.DocumentViewModel _activeDocument;
        [ImportingConstructor]
        public DaxStudioHost(IEventAggregator eventAggregator, Application app)
        {
            _app = app;
            Port = _app.Args().Port;

            if (Port > 0)
            {
                Proxy = new DaxStudio.UI.Model.ProxyPowerPivot(eventAggregator, Port);
            } else
            {
                Proxy = new DaxStudio.UI.Model.ProxyStandalone();
            }
        }

        public bool IsExcel => Proxy.IsExcel;

        public IDaxStudioProxy Proxy { get; }

        public string CommandLineFileName => _app.Args().FileName;

        public int Port { get; }

        public bool DebugLogging => (bool)_app.Args().LoggingEnabled;

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Proxy?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
