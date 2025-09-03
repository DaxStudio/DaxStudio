using System.Collections.Generic;

namespace ADOTabular.Interfaces 
{
    public interface IMetaDataVisitor {
        ADOTabularDatabase Visit(ADOTabularConnection conn);
        SortedDictionary<string, ADOTabularModel> Visit(ADOTabularModelCollection models);
        void Visit(ADOTabularTableCollection tables);
        SortedDictionary<string, ADOTabularColumn> Visit(ADOTabularColumnCollection columns);

        SortedDictionary<string, ADOTabularMeasure> Visit(ADOTabularMeasureCollection measures);

        void Visit(ADOTabularFunctionGroupCollection functionGroups);

        void Visit(ADOTabularKeywordCollection keywords);

        void Visit(MetadataInfo.DaxMetadata daxMetadata);

        void Visit(MetadataInfo.DaxColumnsRemap daxColumnsRemap);

        void Visit(MetadataInfo.DaxTablesRemap daxColumnsRemap);

        void Visit(ADOTabularCalendarCollection calendars);

    }
}
