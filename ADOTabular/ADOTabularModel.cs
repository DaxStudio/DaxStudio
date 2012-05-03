using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADOTabular
{
    public class ADOTabularModel
    {
        private ADOTabularConnection _adoTabConn;
        private string _modelName;
        private ADOTabularTableCollection tableColl;
        public ADOTabularModel(ADOTabularConnection adoTabConn, string modelName)
        {
            _adoTabConn = adoTabConn;
            _modelName = modelName;
        }

        public string Name
        {
            get { return _modelName; }
        }

        public ADOTabularTableCollection Tables
        {
            get {
                if (tableColl == null)
                {
                    tableColl = new ADOTabularTableCollection(_adoTabConn, this);
                }
                return tableColl; 
            }
        }
    }
}
