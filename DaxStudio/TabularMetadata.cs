using ADOTabular;
using System.Windows.Forms;

namespace DaxStudio
{
    public class TabularMetadata
    {
        private enum MetadataImages
        {
            Table         = 0,
            HiddenTable   = 1,
            Column        = 2,
            HiddenColumn  = 3,
            Measure       = 4,
            HiddenMeasure = 5,
            Folder        = 6,
            Function      = 7,
            DmvTable      = 8
        }

        public static void PopulateConnectionMetadata(ADOTabularConnection adoTabularConnection, TreeView tvwMetadata, TreeView tvwFunctions, ListView dmvList, string modelName)
        {
            PopulateModelMetadata(adoTabularConnection, tvwMetadata, modelName);
            PopulateFunctionMetadata(adoTabularConnection, tvwFunctions);
            PopulateDmvList(adoTabularConnection, dmvList);
        }

        public static void PopulateModelMetadata(ADOTabularConnection adoTabularConnection, TreeView tvwMetadata,string modelName)
        {
            tvwMetadata.Nodes.Clear();
            // don't try to populate metadata if we don't have a modelName
            // this possibly means that the table/cube is unprocessed.
            if (modelName == string.Empty) return;
            var m = adoTabularConnection.Database.Models[modelName];
            {
                var modelNode = tvwMetadata.Nodes.Add(m.Name, m.Name, (int)MetadataImages.Folder, (int)MetadataImages.Folder);
                foreach (var t in m.Tables)
                {
                    var tImageId = t.IsVisible ? (int)MetadataImages.Table : (int)MetadataImages.HiddenTable; 
                    var tableNode = modelNode.Nodes.Add(t.Name, t.Caption, tImageId,tImageId);
                    tableNode.ToolTipText = t.Description;
                    foreach (var c in t.Columns)
                    {
                        // add different icons for hidden columns/measures
                        int iImageId;
                        if (c.Type == ADOTabularColumnType.Column)
                        {
                            iImageId = c.IsVisible ? (int)MetadataImages.Column : (int)MetadataImages.HiddenColumn; 
                        }
                        else
                        {
                            iImageId = c.IsVisible ? (int)MetadataImages.Measure : (int)MetadataImages.HiddenMeasure; 
                        }

                        var columnNode = tableNode.Nodes.Add(c.Name, c.Caption, iImageId, iImageId);
                        columnNode.ToolTipText = c.Description;
                    }
                }
                modelNode.Expand();
            }
        }

        public static void PopulateDmvList(ADOTabularConnection connection, ListView list)
        {
            foreach (var dmv in connection.DynamicManagementViews)
            {
                list.Items.Add(dmv.DefaultQuery, dmv.Name, (int) MetadataImages.Table);
            }
        }

        public static void PopulateFunctionMetadata(ADOTabularConnection adoTabularConnection, TreeView tvwFunctions)
        {
            tvwFunctions.Nodes.Clear();
            foreach (ADOTabularFunction f in adoTabularConnection.Functions)
            {
                // ReSharper disable RedundantAssignment
                int groupIndex = -1;
                // ReSharper restore RedundantAssignment
                groupIndex = tvwFunctions.Nodes.IndexOfKey(f.Group);
                var groupNode = groupIndex == -1 ? tvwFunctions.Nodes.Add(f.Group, f.Group, (int)MetadataImages.Folder, (int)MetadataImages.Folder) : tvwFunctions.Nodes[groupIndex];
                groupNode.Nodes.Add(f.Signature, f.Name, (int)MetadataImages.Function, (int)MetadataImages.Function);

            }
        }
    }
}
