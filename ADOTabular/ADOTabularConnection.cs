using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AnalysisServices.AdomdClient;
using System.Data;

namespace ADOTabular
{
    public class ADOTabularConnection
    {
        private AdomdConnection adomdConn; 
        public ADOTabularConnection(string connectionString)
        {
            adomdConn = new AdomdConnection(connectionString);
        }

        // returns the current database for the connection
        public ADOTabularDatabase Database
        {
            get { return new ADOTabularDatabase(this, adomdConn.Database); }
        }

        public override string ToString()
        {
            return adomdConn.ConnectionString;
        }



        // In ADO we set the current DB in the connection string
        // so having a collection of database objects may not be 
        // appropriate
        /*
        private ADOTabularDatabaseCollection adoTabDatabaseColl;
        public ADOTabularDatabaseCollection Databases
        {
            get { 
                if (adoTabDatabaseColl == null)
                {
                    if (adomdConn != null)
                    {
                    adoTabDatabaseColl = new ADOTabularDatabaseCollection(this);
                    }
                    else
                    {
                        throw new NullReferenceException("Unable to populate Databases collection - a valid connection has not been established");
                    }
                }
                return adoTabDatabaseColl;
            }
        }
        */

        public DataSet GetSchemaDataSet(string SchemaName)
        {
            return adomdConn.GetSchemaDataSet(SchemaName, null);
        }

        public DataSet GetSchemaDataSet(string SchemaName, AdomdRestrictionCollection restrictionCollection)
        {
            if (adomdConn.State != ConnectionState.Open)
                adomdConn.Open();
            return adomdConn.GetSchemaDataSet(SchemaName, restrictionCollection);
        }

        public AdomdDataReader ExecuteDaxReader(string query)
        {
            AdomdCommand cmd = adomdConn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = query;
            return cmd.ExecuteReader();
        }

        public DataTable ExecuteDaxQuery(string query)
        {
            AdomdCommand cmd = adomdConn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = query;
            AdomdDataAdapter da = new AdomdDataAdapter(cmd);
            DataTable dt = new DataTable("DAXResult");
            da.Fill(dt);
            return dt;
        }


        public void Close()
        {
            if (adomdConn.State != ConnectionState.Closed && adomdConn.State != ConnectionState.Broken)
            {
                adomdConn.Close();
            }
        }

        private ADOTabularFunctionCollection adoTabFuncColl;
        public ADOTabularFunctionCollection Functions
        {
            get
            {
                if (adoTabFuncColl == null)
                {
                    if (adomdConn != null)
                    {
                        adoTabFuncColl = new ADOTabularFunctionCollection(this);
                    }
                    else
                    {
                        throw new NullReferenceException("Unable to populate Function collection - a valid connection has not been established");
                    }
                }
                return adoTabFuncColl;
            }
        }
    }
}
