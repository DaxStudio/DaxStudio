using System.Collections.Generic;

namespace ADOTabular {
    public interface IMetaDataVisitor {
        SortedDictionary<string, ADOTabularModel> Visit(ADOTabularModelCollection models);
        void Visit(ADOTabularTableCollection tables);
        SortedDictionary<string, ADOTabularColumn> Visit(ADOTabularColumnCollection columns);

        SortedDictionary<string, ADOTabularMeasure> Visit(ADOTabularMeasureCollection measures);

        void Visit(ADOTabularFunctionGroupCollection functionGroups);

        void Visit(ADOTabularKeywordCollection aDOTabularKeywordCollection);

        void Visit(MetadataInfo.DaxMetadata daxMetadata);

    }
}
