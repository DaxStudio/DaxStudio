using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    public class ProxyStandalone : IDaxStudioProxy
    {
        public bool IsExcel
        {
            get { return false; }
        }

        public bool SupportsQueryTable
        {
            get { return false; }
        }

        public bool SupportsStaticTable
        {
            get { return false; }
        }

        public bool HasPowerPivotModel
        {
            get { return false; }
        }

        public void EnsurePowerPivotDataIsLoaded()
        {
            throw new NotImplementedException();
        }

        public string WorkbookName
        {
            get { return string.Empty; }
        }

        public IEnumerable<string> Worksheets
        {
            get { return new List<string>();}
        }

        public Task OutputStaticResultAsync(System.Data.DataTable results, string sheetName)
        {
            throw new NotImplementedException();
        }

        public ADOTabular.ADOTabularConnection GetPowerPivotConnection(string connectionType, string additionalsettings)
        {
            throw new NotImplementedException();
        }

        public int Port { get { return 0; } }
        public void Dispose()
        {
            
        }

        public Task OutputLinkedResultAsync(string daxQuery, string sheetName, string connectionString)
        {
            throw new NotImplementedException();
        }
    }
}
