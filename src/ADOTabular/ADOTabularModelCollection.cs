using ADOTabular.Interfaces;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Serilog;

//using Microsoft.AnalysisServices.AdomdClient;

namespace ADOTabular
{
    public class ADOTabularModelCollection:IEnumerable<ADOTabularModel>
    {
        private readonly IADOTabularConnection _adoTabConn;
        private readonly Guid _diagnosticId = Guid.NewGuid();
        private readonly object _modelsLock = new object();

        public ADOTabularModelCollection(IADOTabularConnection adoTabConn, ADOTabularDatabase database)
        {
            _adoTabConn = adoTabConn;
            Database = database;
            //_models = _adoTabConn.Visitor.Visit(this);
        }

        // True once the model list has been materialized (the DISCOVER query has run). Callers can
        // check this to avoid triggering the blocking query on the UI thread.
        public bool IsMaterialized => _models != null;

        private SortedDictionary<string,ADOTabularModel> InternalModelCollection
        {
            get
            {
                lock (_modelsLock)
                {
                    if (_models == null)
                    {
                        var operationId = Guid.NewGuid();
                        var connectionId = (_adoTabConn as ADOTabularConnection)?.DiagnosticId;
                        Log.Debug("{class} {method} Model discovery start: CollectionId={CollectionId} ConnectionId={ConnectionId} OperationId={OperationId} ThreadId={ThreadId}",
                            nameof(ADOTabularModelCollection), nameof(InternalModelCollection), _diagnosticId, connectionId, operationId, System.Threading.Thread.CurrentThread.ManagedThreadId);
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var succeeded = false;
                        try
                        {
                            _models = _adoTabConn.Visitor.Visit(this);
                            succeeded = true;
                        }
                        finally
                        {
                            sw.Stop();
                            Log.Debug("{class} {method} Model discovery end: CollectionId={CollectionId} ConnectionId={ConnectionId} OperationId={OperationId} ElapsedMs={ElapsedMs} Succeeded={Succeeded} ThreadId={ThreadId}",
                                nameof(ADOTabularModelCollection), nameof(InternalModelCollection), _diagnosticId, connectionId, operationId, sw.ElapsedMilliseconds, succeeded, System.Threading.Thread.CurrentThread.ManagedThreadId);
                        }
                    }
                    return _models;
                }
            }
        }

        public void Add(ADOTabularModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            lock (_modelsLock)
            {
                if (_models == null)
                    _models = new SortedDictionary<string, ADOTabularModel>();
                _models.Add(model.Name, model);
            }
        }

        public ADOTabularDatabase Database { get; }

        public ADOTabularModel BaseModel
        {
            get
            { return InternalModelCollection.Values.FirstOrDefault(m => !m.IsPerspective); }
        }

        public ADOTabularModel this[string modelName] => InternalModelCollection[modelName];
        //return (from dr in GetModelsTable().Rows.Cast<DataRow>() where string.Compare(modelName, dr["CUBE_NAME"].ToString(), StringComparison.InvariantCultureIgnoreCase) == 0 select new ADOTabularModel(_adoTabConn, dr)).FirstOrDefault();
        // todo - should we return a model not found exception instead of null?

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

                throw new ArgumentException($"Index of {index} is outside the range of this collection");

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

        private volatile SortedDictionary<string,ADOTabularModel> _models;

        public IEnumerator<ADOTabularModel> GetEnumerator()
        {
            foreach (ADOTabularModel m in InternalModelCollection.Values)
            {
                yield return m;

            }   
        }

        public void Clear()
        {
            lock (_modelsLock)
            {
                InternalModelCollection.Clear();
            }
            //throw new NotImplementedException();
        }
    }
}
