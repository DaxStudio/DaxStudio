using System.Collections.Generic;
using ADOTabular.Interfaces;
using Microsoft.AnalysisServices.Tabular;

namespace ADOTabular
{
    public class ADOTabularModel
    {
        private readonly IADOTabularConnection _adoTabConn;
        private ADOTabularTableCollection _tableColl;
        private readonly object tableLock = new object();

        public ADOTabularModel(IADOTabularConnection adoTabConn, ADOTabularDatabase database, string name, string caption, string description, string baseModelName)
        {
            _adoTabConn = adoTabConn;
            Database = database;
            Name = name;
            Caption = caption;
            Description = description;
            BaseModelName = baseModelName;
            Roles = new Dictionary<string, ADOTabularColumn>();
            Relationships = new List<ADOTabularRelationship>();
            TOMModel = new Model(){Name = name};
            MeasureExpressions = new Dictionary<string, string>();
            MeasureFormatStringExpressions = new Dictionary<string, string>();
        }
        public ADOTabularDatabase Database { get; }
        public string BaseModelName { get; private set; }

        public bool IsPerspective => !string.IsNullOrEmpty(BaseModelName);

        public string Name { get; private set; }

        public string Caption { get; private set; }
        public string Description { get; private set; }

        public ADOTabularTableCollection Tables
        {
            get {
                if (_tableColl == null)
                {
                    lock (tableLock)
                    {
                        _tableColl ??= new ADOTabularTableCollection(_adoTabConn, this);
                    }
                }
                return _tableColl;
            }
        }

        private ADOTabularCalendarCollection _calendars;
        public ADOTabularCalendarCollection Calendars
        {
            get
            {
                if (_calendars == null)
                {
                    lock (tableLock)
                    {
                        _calendars ??= new ADOTabularCalendarCollection(_adoTabConn);
                    }
                }
                return _calendars;
            }
        }

        public Dictionary<string,ADOTabularColumn> Roles { get; private set; }

        public List<ADOTabularRelationship> Relationships { get; private set; }
        public Model TOMModel { get; }

        public MetadataImages MetadataImage => IsPerspective? MetadataImages.Perspective : MetadataImages.Model;

        public string Culture { get; private set; }

        internal void AddRole(ADOTabularColumn col)
        {
            var roleId = col.Role;
            if (Roles.ContainsKey(roleId))
            {
                var suffix = 2;
                while (Roles.ContainsKey($"{col.Role}{suffix}")) {
                    suffix++;
                }
                roleId = $"{col.Role}{suffix}";
            }
            col.Role = roleId;
            Roles.Add(roleId, col);
        }

        public ADOTabularModelCapabilities Capabilities { get; set; } = new ADOTabularModelCapabilities();
        
        public double CSDLVersion { get; set; }

        public Dictionary<string, string> MeasureExpressions { get; }
        public Dictionary<string, string> MeasureFormatStringExpressions { get; }
    }
}
