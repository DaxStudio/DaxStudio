using System.Collections.Generic;

namespace ADOTabular
{
    public interface IMetaDataVisitor
    {
        SortedDictionary<string,ADOTabularModel> Visit(ADOTabularModelCollection models);
        void Visit(ADOTabularTableCollection tables);
        SortedDictionary<string,ADOTabularColumn> Visit(ADOTabularColumnCollection columns);
        void Visit(ADOTabularFunctionGroupCollection functionGroups);
    }
}
