using ADOTabular;
using System.Windows.Forms;

namespace DaxStudio
{
    public class TabularMetadata
    {
        private enum MetadataImages
        {
            Table = 0,
            Column = 1,
            HiddenColumn = 2,
            Measure = 3,
            HiddenMeasure = 4,
            Folder = 5,
            Function = 6
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

            var m = adoTabularConnection.Database.Models[modelName];
            {
                var modelNode = tvwMetadata.Nodes.Add(m.Name, m.Name, (int)MetadataImages.Folder, (int)MetadataImages.Folder); //todo - add image index
                foreach (var t in m.Tables)
                {
                    var tableNode = modelNode.Nodes.Add(t.Name, t.Caption, (int)MetadataImages.Table, (int)MetadataImages.Table);
                    foreach (var c in t.Columns)
                    {
                        // add different icons for hidden columns/measures
                        // ReSharper disable RedundantAssignment
                        var iImageId = 0;
                        // ReSharper restore RedundantAssignment
                        if (c.Type == ADOTabularColumnType.Column)
                        {
                            if (c.IsVisible)
                            { iImageId = (int)MetadataImages.Column; }
                            else
                            { iImageId = (int)MetadataImages.HiddenColumn; }
                        }
                        else
                        {
                            if (c.IsVisible)
                            { iImageId = (int)MetadataImages.Measure; }
                            else
                            { iImageId = (int)MetadataImages.HiddenMeasure; }
                        }

                        tableNode.Nodes.Add(c.Name, c.Caption, iImageId, iImageId);
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
