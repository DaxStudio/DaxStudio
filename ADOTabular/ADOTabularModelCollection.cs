using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

//using Microsoft.AnalysisServices.AdomdClient;

namespace ADOTabular
{
    public class ADOTabularModelCollection:IEnumerable<ADOTabularModel>
    {
        private readonly ADOTabularConnection _adoTabConn;
        private readonly ADOTabularDatabase  _database;
        public ADOTabularModelCollection(ADOTabularConnection adoTabConn, ADOTabularDatabase database)
        {
            _adoTabConn = adoTabConn;
            _database = database;
            _models = _adoTabConn.Visitor.Visit(this);
        }

        public ADOTabularDatabase Database
        {
            get { return _database; }
        }

        public ADOTabularModel BaseModel
        {
            get
            { return _models.Values.FirstOrDefault(m => !m.IsPerspective); }
        }

        public ADOTabularModel this[string modelName]
        {
            get
            {
                return _models[modelName];
                //return (from dr in GetModelsTable().Rows.Cast<DataRow>() where string.Compare(modelName, dr["CUBE_NAME"].ToString(), StringComparison.InvariantCultureIgnoreCase) == 0 select new ADOTabularModel(_adoTabConn, dr)).FirstOrDefault();
                // todo - should we return a model not found exception instead of null?
            }
        }

        public ADOTabularModel this[int index]
        {
            get
            {
                int i = 0;
                foreach (var m in _models.Values)
                {
                    if (i == index)
                    {
                        return m;
                    }
                    i++;
                }
                    
                throw new IndexOutOfRangeException();

                //return (from dr in GetModelsTable().Rows.Cast<DataRow>() where string.Compare(modelName, dr["CUBE_NAME"].ToString(), StringComparison.InvariantCultureIgnoreCase) == 0 select new ADOTabularModel(_adoTabConn, dr)).FirstOrDefault();
                // todo - should we return a model not found exception instead of null?
            }
        }

        public int Count
        {
            get { return _models.Count; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator(); 
        }

        private Dictionary<string,ADOTabularModel> _models;  

        public IEnumerator<ADOTabularModel> GetEnumerator()
        {
            foreach (ADOTabularModel m in _models.Values)
            {
                yield return m;

            }   
        }
        
    }
}
