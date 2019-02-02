using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADOTabular
{
    
    public class ADOTabularHierarchy:ADOTabularColumn
    {
        private List<ADOTabularLevel> _levels;
        private string _structure;

        public ADOTabularHierarchy( ADOTabularTable table, string internalName,string name, string caption,  string description,
                                bool isVisible, ADOTabularObjectType columnType, string contents, string structure)
        :base(table,internalName,name, caption,description,isVisible,columnType,contents)
        {
            _levels = new List<ADOTabularLevel>();
            _structure = structure;
            if (structure == "Unnatural")
            {
                if (description.Length > 0) Description += '\n';
                Description += "WARNING: Unnatural Hierarchy - may have a negative performance impact";
                ObjectType = ADOTabularObjectType.UnnaturalHierarchy;
            }
        }
        public List<ADOTabularLevel> Levels { get { return _levels; } }
        public string Structure { get { return _structure; } }
        public override string DaxName
        {
            get
            {
                return "";
            }
        }

    }
}
