using ADOTabular;
using ADOTabular.Interfaces;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DaxStudio.UI.Model
{
    [JsonObject(MemberSerialization.OptIn)]
    public class QueryBuilderFilterList :  IQueryBuilderFieldList
    {
        public QueryBuilderFilterList(Func<IModelCapabilities> modelCapabilities)
        {
            DropHandler = new QueryBuilderDropHandler(this);
            GetModelCapabilities = modelCapabilities;
        }

        public void Remove(QueryBuilderFilter item)
        {
            Items.Remove(item);
        }
        [JsonProperty]
        public ObservableCollection<QueryBuilderFilter> Items { get; } = new ObservableCollection<QueryBuilderFilter>();
        public QueryBuilderDropHandler DropHandler { get; }
        public Func<IModelCapabilities> GetModelCapabilities { get; }

        public IEnumerable<FilterType> FilterTypes
        {
            get
            {
                var items = Enum.GetValues(typeof(FilterType)).Cast<FilterType>();
                return items;
            }
        }



        #region IQueryBuilderFieldList
        public void Add(IADOTabularColumn item)
        {
            var filter = new QueryBuilderFilter(item, GetModelCapabilities());
            Items.Add(filter);
        }

        internal void Add(QueryBuilderFilter filter)
        {
            Items.Add(filter);
        }

        public bool Contains(IADOTabularColumn item)
        {
            return Items.FirstOrDefault(f => f.TabularObject == item) != null;
        }
        public int Count => Items.Count;

        public int IndexOf(IADOTabularColumn obj)
        {
            var item = Items.FirstOrDefault(f => f.TabularObject == obj);
            return Items.IndexOf(item);
        }
        public void Insert(int index, IADOTabularColumn item)
        {
            var filter = new QueryBuilderFilter(item, GetModelCapabilities());
            // if we are 'inserting' at the end just do an add
            if (index >= Items.Count) Items.Add(filter);
            Items.Insert(index, filter);
        }
        public void Move(int oldIndex, int newIndex)
        {
            Items.Move(oldIndex, newIndex);
        }
        #endregion

        public void Clear()
        {
            Items.Clear();
        }
    }
}
