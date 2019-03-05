using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular.Utils
{
    public static class MetadataImageHelper
    {
        public static MetadataImages GetMetadataImage(this IADOTabularObject tabObj)
        {
            switch (tabObj.ObjectType)
            {
                case ADOTabularObjectType.Column:
                    return tabObj.IsVisible ? MetadataImages.Column : MetadataImages.HiddenColumn;
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
                default:
                    return MetadataImages.Unknown;
            }
        }
    }
}
