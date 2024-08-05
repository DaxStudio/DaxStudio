using DaxStudio.UI.Interfaces;
using System;

namespace DaxStudio.CommandLine.UIStubs
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
