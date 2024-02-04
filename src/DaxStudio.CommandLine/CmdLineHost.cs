using DaxStudio.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine
{
    internal class CmdLineHost : IDaxStudioHost
    {
        public IDaxStudioProxy Proxy => throw new NotImplementedException();

        public bool IsExcel => false;

        public string CommandLineFileName => throw new NotImplementedException();

        public int Port => 0;

        public bool DebugLogging => false;

        public void Dispose()
        {
            // n/a
        }
    }
}
