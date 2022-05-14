using ADOTabular;

namespace DaxStudio.UI.Extensions
{
    public static class ADOTabularTableExtensions
    {
        public static string ImageResource(this ADOTabularTable table)
        {
            switch (table.MetadataImage)
            {
                case MetadataImages.Column:
                    return "column";

                //case MetadataImages.Database:
                case MetadataImages.Folder:
                case MetadataImages.HiddenColumn:
                case MetadataImages.HiddenMeasure:

                case MetadataImages.Hierarchy:
                case MetadataImages.Kpi:
                case MetadataImages.Measure:
                    return "measure";
                case MetadataImages.HiddenTable:
                case MetadataImages.Table:

                    return table.IsDateTable ? "date_tableDrawingImage" : "tableDrawingImage";
                case MetadataImages.UnnaturalHierarchy:
                    return "";
            }
            return "";
        }
    }
}
