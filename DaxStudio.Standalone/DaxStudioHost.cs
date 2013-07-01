using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using ADOTabular;
using DaxStudio;
using DaxStudio.UI;
using DaxStudio.Interfaces;

namespace DaxStudio.Standalone
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IDaxStudioHost))]
    public class DaxStudioHost : IDaxStudioHost
    {
        
        public bool IsExcel {
            get { return false; }
        }
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
        public List<string> Worksheets {
            get { return null; }
        }

        public ADOTabularConnection GetPowerPivotConnection()
        {
            //Todo 
            throw new NotImplementedException();
        }
    }
}
