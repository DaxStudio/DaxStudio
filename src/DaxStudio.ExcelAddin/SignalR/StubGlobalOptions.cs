using DaxStudio.Interfaces;
using DaxStudio.Interfaces.Enums;
using System;
using System.Collections.ObjectModel;
using System.Security;

namespace DaxStudio.SignalR
{
    class StubGlobalOptions : IGlobalOptionsBase
    {
        public bool TraceDirectQuery
        {
            get => false;

            set => throw new NotImplementedException();
        }

    }
}
