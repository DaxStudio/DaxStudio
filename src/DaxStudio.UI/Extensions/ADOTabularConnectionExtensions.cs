using DaxStudio.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Extensions
{
    public static class ADOTabularConnectionExtensions
    {
        public static bool ShouldAutoRefreshMetadata(this ADOTabular.ADOTabularConnection conn, IGlobalOptions options)
        {
            switch( conn.ConnectionType)
            {
                case ADOTabular.ADOTabularConnectionType.Cloud:
                    return options.AutoRefreshMetadataCloud;
                case ADOTabular.ADOTabularConnectionType.LocalNetwork:
                    return options.AutoRefreshMetadataLocalNetwork;
                case ADOTabular.ADOTabularConnectionType.LocalMachine:
                    return options.AutoRefreshMetadataLocalMachine;
                default:
                    return true;
            }
        }
    }
}
