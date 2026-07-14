using System;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class ConnectCommand : ScriptCommand
    {
        public ConnectCommand(string serverType, string serverName)
        {
            try
            {
                ConnectionType = (ConnectionType)System.Enum.Parse(typeof(ConnectionType), serverType, true);
            } 
            catch 
            {
                //throw new ArgumentException($"Unable to process CONNECT command '{serverType}' is not a valid ConnectionType");
            }
            ConnectionName = serverName;
        }
        public ConnectionType ConnectionType {get;}
        public string ConnectionName { get;  }
    }
}
