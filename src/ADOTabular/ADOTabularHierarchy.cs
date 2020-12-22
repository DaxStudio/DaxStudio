using System;
using System.Collections.Generic;

namespace ADOTabular
{
    
    public class ADOTabularHierarchy:ADOTabularColumn
    {
        public ADOTabularHierarchy( ADOTabularTable table, string internalName,string name, string caption,  string description,
                                bool isVisible, ADOTabularObjectType columnType, string contents, string structure)
        :base(table,internalName,name, caption,description,isVisible,columnType,contents)
        {
            if (description == null) throw new ArgumentNullException(nameof(description));
            Levels = new List<ADOTabularLevel>();
            Structure = structure;
            if (structure == "Unnatural")
            {
                if (description.Length > 0) Description += '\n';
                Description += "WARNING: Unnatural Hierarchy - may have a negative performance impact";
                ObjectType = ADOTabularObjectType.UnnaturalHierarchy;
            }
        }
        public List<ADOTabularLevel> Levels { get; }
        public string Structure { get; }
        public override string DaxName
        {
            get
            {
                return "";
            }
        }

    }
}
