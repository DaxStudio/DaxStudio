using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;
using ADOTabular;
using DaxStudio;
using DaxStudio.UI;
using DaxStudio.Interfaces;
using System.Data;
using Caliburn.Micro;

namespace DaxStudio.Standalone
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IDaxStudioHost))]
    public class DaxStudioHost : IDaxStudioHost
    {
        private int _port;
        private IDaxStudioProxy _proxy;
        private IEventAggregator _eventAggregator;
        [ImportingConstructor]
        public DaxStudioHost(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                int.TryParse(args[1], out _port);
            }
            if (_port > 0)
            {
                _proxy = new DaxStudio.UI.Model.ProxyPowerPivot(_eventAggregator, _port);
            }
            else
            {
                _proxy = new DaxStudio.UI.Model.ProxyStandalone();
            }
        }

        public bool IsExcel {
            get { return _proxy.IsExcel; }
        }

        public IDaxStudioProxy Proxy
        {
            get { return _proxy; }
        }

 /*
        public bool SupportsQueryTable { get { return false; } }
        public bool SupportsStaticTable { get { return false; } }
        public bool HasPowerPivotModel { get { return false; } }
        public bool HasPowerPivotData()
        {
            return false;
        }

        public bool EnsurePowerPivotDataIsLoaded()
        {
            return false;
        }

        public string BuildPowerPivotConnection()
        {
            throw new NotImplementedException();
        }

        public string WorkbookName {
            get { return "No Workbook available"; }
            set {}
        }
        */

        /* ==== Excel Host Stub functions - do not need to be implemented in the stand alone host  ==== */

        /*
        public void OutputStaticResult(DataTable results, string sheetName)
        {
            throw new NotImplementedException();
        }

        public void OutputQueryTableResult(string connection, string daxQuery, string sheetName, IQueryRunner runner)
        {
            throw new NotImplementedException();
        }

        public ADOTabularConnection GetPowerPivotConnection()
        {
            //Todo 
            throw new NotImplementedException();
        }

        void IDaxStudioHost.EnsurePowerPivotDataIsLoaded()
        {
            throw new NotImplementedException();
        }

        IEnumerable<string> IDaxStudioHost.Worksheets
        {
            get { return new List<string>(); }
        }

*/
        public void Dispose()
        {
            
        }
  
    }
}
