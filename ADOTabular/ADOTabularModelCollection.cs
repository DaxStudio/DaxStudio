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
            //_models = _adoTabConn.Visitor.Visit(this);
        }

        private Dictionary<string,ADOTabularModel> InternalModelCollection
        {
            get
            {
                if (_models == null)
                {
                    _models = _adoTabConn.Visitor.Visit(this);
                }
                return _models;
            }
        }

        public void Add(ADOTabularModel model)
        {
            if (_models == null)
                _models = new Dictionary<string, ADOTabularModel>();
            _models.Add(model.Name, model);
        }

        public ADOTabularDatabase Database
        {
            get { return _database; }
        }

        public ADOTabularModel BaseModel
        {
            get
            { return InternalModelCollection.Values.FirstOrDefault(m => !m.IsPerspective); }
        }

        public ADOTabularModel this[string modelName]
        {
            get
            {
                return InternalModelCollection[modelName];
                //return (from dr in GetModelsTable().Rows.Cast<DataRow>() where string.Compare(modelName, dr["CUBE_NAME"].ToString(), StringComparison.InvariantCultureIgnoreCase) == 0 select new ADOTabularModel(_adoTabConn, dr)).FirstOrDefault();
                // todo - should we return a model not found exception instead of null?
            }
        }

        public ADOTabularModel this[int index]
        {
            get
            {
                int i = 0;
                foreach (var m in InternalModelCollection.Values)
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
            get { return InternalModelCollection.Count; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator(); 
        }

        private Dictionary<string,ADOTabularModel> _models;  

        public IEnumerator<ADOTabularModel> GetEnumerator()
        {
            foreach (ADOTabularModel m in InternalModelCollection.Values)
            {
                yield return m;

            }   
        }
        
    }
}
