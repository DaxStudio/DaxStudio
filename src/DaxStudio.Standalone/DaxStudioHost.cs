using System;
using System.ComponentModel.Composition;
using DaxStudio.Interfaces;
using Caliburn.Micro;
using Serilog;
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
        private readonly IEventAggregator _eventAggregator;
        private readonly Application _app;

        //private UI.ViewModels.DocumentViewModel _activeDocument;
        [ImportingConstructor]
        public DaxStudioHost(IEventAggregator eventAggregator, Application app)
        {
            _eventAggregator = eventAggregator;
            _app = app;
            Port = _app.Args().Port;

            if (Port > 0)
            {
                Proxy = new DaxStudio.UI.Model.ProxyPowerPivot(_eventAggregator, Port);
            } else
            {
                Proxy = new DaxStudio.UI.Model.ProxyStandalone();
            }


            //string[] args = Environment.GetCommandLineArgs();
            //if (args.Length > 1)
            //{
            //    int.TryParse(args[1], out _port);
            //}
            //if (_port > 0)
            //{
            //    Log.Debug("{class} {method} {message} {port}", "DaxStudioHost", "ctor", "Constructing ProxyPowerPivot", _port);
            //    _proxy = new DaxStudio.UI.Model.ProxyPowerPivot(_eventAggregator, _port);
            //}
            //else
            //{
            //    // pass along commandline to UI
            //    Log.Debug("{class} {method} {message}", "DaxStudioHost", "ctor", "constructing ProxyStandalone");
            //    if (args.Length > 1) {
            //        if (args[1].ToLower() != "-log" ) _commandLineFileName = args[1];
            //    }
            //    _proxy = new DaxStudio.UI.Model.ProxyStandalone();
            //}
        }

        public bool IsExcel {
            get { return Proxy.IsExcel; }
        }

        public IDaxStudioProxy Proxy { get; }

        public string CommandLineFileName
        {
            get { return  _app.Args().FileName; }
        }

        public int Port { get; }

        public bool DebugLogging
        {
            get
            {
                return (bool)_app.Args().LoggingEnabled;
            }
        }

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
