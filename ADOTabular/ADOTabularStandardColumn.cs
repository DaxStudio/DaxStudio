using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADOTabular
{
    public class ADOTabularStandardColumn: ADOTabularColumn
    {
                public ADOTabularStandardColumn( ADOTabularTable table, string internalName, string caption,  string description,
                                bool isVisible, ADOTabularColumnType columnType, string contents)
        :base(table,internalName,caption,description,isVisible,columnType,contents)

    {}
    }
}
