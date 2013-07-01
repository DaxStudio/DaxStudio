using System.Collections.Generic;

namespace ADOTabular
{
    public interface IMetaDataVisitor
    {
        Dictionary<string,ADOTabularModel> Visit(ADOTabularModelCollection models);
        Dictionary<string,ADOTabularTable> Visit(ADOTabularTableCollection tables);
        Dictionary<string,ADOTabularColumn> Visit(ADOTabularColumnCollection columns);
        void Visit(ADOTabularFunctionGroupCollection functionGroups);
    }
}
