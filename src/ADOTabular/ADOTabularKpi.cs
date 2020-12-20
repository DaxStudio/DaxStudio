using System.Collections.Generic;

namespace ADOTabular
{
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct KpiDetails
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public string Goal { get; set; }
        public string Status { get; set; }
        public string Graphic { get; set; }

        public bool IsBlank()
        {
            return string.IsNullOrEmpty(Goal) && string.IsNullOrEmpty(Status) && string.IsNullOrEmpty(Graphic);
        }


    }
    public class ADOTabularKpi: ADOTabularColumn
    {
        private KpiDetails _kpi;
        public ADOTabularKpi( ADOTabularTable table,string internalName, string name, string caption,  string description,
                                bool isVisible, ADOTabularObjectType columnType, string contents, KpiDetails kpi)
        :base(table, internalName,name, caption,description,isVisible,columnType,contents)
        {
            _kpi = kpi;
            
        }


        private List<ADOTabularKpiComponent> _components;
        public List<ADOTabularKpiComponent> Components
        {
            get
            {
                if (_components == null)
                {
                    _components = new List<ADOTabularKpiComponent>();
                    _components.AddRange(new[] 
                    {new ADOTabularKpiComponent(this,KpiComponentType.Value ),
                    new ADOTabularKpiComponent(this.Table.Columns.GetByPropertyRef(_kpi.Goal), KpiComponentType.Goal),
                    new ADOTabularKpiComponent(this.Table.Columns.GetByPropertyRef(_kpi.Status), KpiComponentType.Status)
                });
                }
                return _components;
            }
        }
        public ADOTabularColumn Goal { 
            get {
                if (_kpi.IsBlank()) return null;
                if (string.IsNullOrEmpty(_kpi.Goal)) return null;
                return Table.Columns.GetByPropertyRef(_kpi.Goal);
            }
        }
        public ADOTabularColumn Status { 
            get {
                if (_kpi.IsBlank()) return null;
                if (string.IsNullOrEmpty(_kpi.Status)) return null;
                return Table.Columns.GetByPropertyRef(_kpi.Status);
            } 
        }

    }
}
