using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Excel = Microsoft.Office.Interop.Excel;


namespace DaxStudio
{
    public class ServerConnections
    {
        List<ServerConnection> _connections = new List<ServerConnection>();
        private Excel.Application app = new Excel.Application();

        public ServerConnections() { }

        public ServerConnections(string ServerName, string ModelName, string ConnectionString )
        {

            Excel.Workbook wb = app.ActiveWorkbook;
            string wrkbkPath = wb.FullName;
            _connections.Add(new ServerConnection(ServerName, ModelName, ConnectionString));

        }

        public void AddConnection(string ServerName, string ModelName, string ConnectionString)
        {
            _connections.Add(new ServerConnection(ServerName, ModelName, ConnectionString));
        }

        public int Length
        {
            get { return _connections.Count; }
        }

        public string ServerName(string ModelName)
        {
            for (int i = 0; i < this.Length; i++)
            {
                if (_connections[i].ModelName == ModelName)
                    return _connections[i].ServerName;
            }
            return null;
        }

        public string ConnectionString(string ModelName)
        {
            for (int i = 0; i < this.Length; i++)
            {
                if (this[i].ModelName == ModelName)
                    return this[i].ConnectionString;
            }

            return "";
        }

        public ServerConnection this[int Index]
        {
            get
            {
                if (Index < 0 || Index >= this.Length )
                    return null;
                else
                    return _connections[Index];
            }
        }

    }

    public class ServerConnection
    {
        private string _serverName = "";
        private  string _modelName = "";
        private string _connectionString = "";

        public ServerConnection(string ServerName, string ModelName, string ConnectionString)
        {
            _serverName  = ServerName;
            _modelName = ModelName;
            _connectionString = ConnectionString;
        }

        public string ServerName 
        {
            get { return _serverName; }
            set { _serverName = value; }
        }

        public string ModelName
        {
            get { return _modelName; }
            set { _modelName = value; }
        }

        public string ConnectionString
        {
            get { return _connectionString; }
            set { _connectionString = value; }
        }


    }
}
