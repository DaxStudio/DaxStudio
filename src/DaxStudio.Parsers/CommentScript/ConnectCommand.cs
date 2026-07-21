using System;
using System.IO;

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
        public string ConnectionName { get; set; }

        /// <summary>
        /// True when this is a DESKTOP connection whose name is a path to a .pbix file
        /// (either a rooted path or a value ending in ".pbix"). In that case
        /// <see cref="FilePath"/> holds the full path and <see cref="InstanceName"/>
        /// is the file name without its extension.
        /// </summary>
        public bool IsFilePath =>
            ConnectionType == ConnectionType.DESKTOP
            && !string.IsNullOrWhiteSpace(ConnectionName)
            && (ConnectionName.EndsWith(".pbix", StringComparison.OrdinalIgnoreCase)
                || Path.IsPathRooted(ConnectionName));

        /// <summary>The full .pbix path when <see cref="IsFilePath"/> is true, otherwise null.</summary>
        public string FilePath => IsFilePath ? ConnectionName : null;

        /// <summary>
        /// The name used to match a running Power BI Desktop instance (its title-bar
        /// report name). When a file path is supplied this is the file name without
        /// its extension; otherwise it is the raw connection name.
        /// </summary>
        public string InstanceName =>
            IsFilePath ? Path.GetFileNameWithoutExtension(FilePath) : ConnectionName;
    }
}
