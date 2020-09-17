using System;
using ADOTabular.Enums;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.Tests.Mocks
{
    class MockDaxStudioHost : IDaxStudioHost
    {
        public string CommandLineFileName
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public AdomdType ConnectionType
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool DebugLogging
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool IsExcel
        {
            get
            {
                return false;
            }
        }

        public int Port
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public IDaxStudioProxy Proxy
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
