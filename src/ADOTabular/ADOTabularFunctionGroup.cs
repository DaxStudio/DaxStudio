using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADOTabular
{
    public class ADOTabularFunctionGroup
    {
        private readonly ADOTabularConnection _connection;
        public ADOTabularFunctionGroup(string caption, ADOTabularConnection connection)
        {
            Caption = caption;
            _connection = connection;
            Functions = new ADOTabularFunctionCollection(_connection);
        }
        public string Caption { get; private set; }
        public ADOTabularFunctionCollection Functions { get; private set; }
        public MetadataImages MetadataImage
        {
            get { return MetadataImages.Folder; }
        }
    }
}
