using ADOTabular.Interfaces;
using System.Diagnostics.Contracts;

namespace ADOTabular.Utils
{
    public static class MetadataImageHelper
    {
        public static MetadataImages GetMetadataImage(this IADOTabularObject tabObj)
        {
            Contract.Requires(tabObj != null, "The tabObj parameter must not be null");
            switch (tabObj.ObjectType)
            {
                case ADOTabularObjectType.Column:
                    return tabObj.IsVisible ? MetadataImages.Column : MetadataImages.HiddenColumn;
                case ADOTabularObjectType.MeasureFormatString:
                case ADOTabularObjectType.Measure:
                    return tabObj.IsVisible ? MetadataImages.Measure : MetadataImages.HiddenMeasure;
                case ADOTabularObjectType.Table:
                    return tabObj.IsVisible ? MetadataImages.Table : MetadataImages.HiddenTable;
                case ADOTabularObjectType.DMV:
                    return MetadataImages.DmvTable;
                case ADOTabularObjectType.Folder:
                    return MetadataImages.Folder;
                case ADOTabularObjectType.Function:
                    return MetadataImages.Function;
                case ADOTabularObjectType.Hierarchy:
                    return MetadataImages.Hierarchy;
                case ADOTabularObjectType.KPI:
                    return MetadataImages.Kpi;
                case ADOTabularObjectType.KPIGoal:
                case ADOTabularObjectType.KPIStatus:
                    return MetadataImages.Measure;
                case ADOTabularObjectType.UnnaturalHierarchy:
                    return MetadataImages.UnnaturalHierarchy;
                case ADOTabularObjectType.Level:
                    return MetadataImages.Column;
                case ADOTabularObjectType.Calendar:
                    return MetadataImages.Calendar;
                default:
                    return MetadataImages.Unknown;
            }
        }
    }
}
