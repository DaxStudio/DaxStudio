using ADOTabular;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using ADOTabular.Utils;
using DaxStudio.UI.Interfaces;
using Serilog;

namespace DaxStudio.UI.Model
{

    public static class ADOTabularFunctionsExtensions
    {

        public static List<FilterableTreeViewItem> TreeViewFunctions(this ADOTabularConnection conn, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane)
        {
            var lst = new List<FilterableTreeViewItem>();
            foreach (var fg in conn.FunctionGroups)
            {
                
                lst.Add(new TreeViewFunctionGroup(fg, fg.TreeViewFunctions, options, eventAggregator, metadataPane));
            }
            return lst;
        }

        public static IEnumerable<FilterableTreeViewItem> TreeViewFunctions(this ADOTabularFunctionGroup group, IADOTabularObject tabularObject,  IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane)
        {
            var lst = new SortedList<string, FilterableTreeViewItem>();
            foreach (var f in group.Functions)
            {

                var fun = new TreeViewFunction(f, null, options, eventAggregator, metadataPane);

                var lstItem = lst.FirstOrDefault(x => x.Value.Name == fun.Name).Value;
                if (lstItem != null && lstItem.ObjectType == lstItem.ObjectType)
                {
                    // todo add this col as a child of lstItem
                    throw new NotSupportedException();
                }
                else
                {
                    lst.Add(f.Caption, fun);
                }
            }



            return lst.Values;
        }



        //public static IEnumerable<FilterableTreeViewItem> TreeViewFolderChildren(this IADOTabularObjectReference objRef, ADOTabularTable table, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane)
        //{
        //    var lst = new List<FilterableTreeViewItem>();

        //    var folder = objRef as IADOTabularFolderReference;

        //    if (folder != null)
        //    {
        //        foreach (var folderItem in folder.FolderItems)
        //        {
        //            GetChildrenDelegate getChildren = null;
        //            if (folderItem is IADOTabularFolderReference) getChildren = ((IADOTabularObjectReference)folderItem).TreeViewFolderChildren;
        //            lst.Add(new TreeViewColumn(folderItem, getChildren, table, options, eventAggregator, metadataPane));
        //        }
        //    }
        //    else
        //    {
        //        var col = table.Columns.GetByPropertyRef(objRef.InternalReference);
        //        lst.Add(new TreeViewColumn(col, null, options, eventAggregator,metadataPane));
        //    }


        //    return lst;
        //}


        //}

        //public delegate IEnumerable<FilterableTreeViewItem> GetChildrenDelegate(IGlobalOptions options, IEventAggregator eventAggregator);
        public delegate IEnumerable<FilterableTreeViewItem> GetFunctionsDelegate(ADOTabularFunctionGroup group, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane);


        public class TreeViewFunctionGroup : FilterableTreeViewItem, IADOTabularObject
        {
            private ADOTabularFunctionGroup _functionGroup;

            public TreeViewFunctionGroup(ADOTabularFunctionGroup functionGroup, GetChildrenDelegate getChildren, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane) : base(getChildren, options, eventAggregator, metadataPane)
            {
                _functionGroup = functionGroup;
            }


            public MetadataImages MetadataImage { get { return MetadataImages.Folder; } }

            // the Caption is affected by translations, it is visible in resultsets, but is not used in queries
            public string Caption => _functionGroup.Caption;
            // the Name property is the untranslated object name used in queries and DAX expressions
            public override string Name => _functionGroup.Caption;
            public override ADOTabularObjectType ObjectType => ADOTabularObjectType.Table;
            public string Description { get { return _functionGroup.Caption; } }
            public override bool IsVisible => true;
            public override bool IsCriteriaMatched(string criteria)
            {
                //    if (!this.MetadataPane.ShowHiddenObjects && !this.IsVisible) return false;
                return String.IsNullOrEmpty(criteria) || Caption.IndexOf(criteria, StringComparison.InvariantCultureIgnoreCase) >= 0;
            }

            // this the fully qualified (and possibly quoted)
            // so for a column it would be something like 'table name'[column name]
            // but for a table it would be 'table name'
            public string DaxName => string.Empty;

        }

        //public static class TreeViewColumnFactory
        //{
        //    public static FilterableTreeViewItem Create(ADOTabularColumn col) {
        //        //TODO create folder hierarchy if DisplayFolder is not empty string
        //        // else return raw column
        //        return null;
        //    }
        //}

        public class TreeViewFunction : FilterableTreeViewItem, IADOTabularObject
        {
            /*
            Folder

            Hierarchy
              Caption
              Description
            Level -> Column
              Caption
              DataTypeName
              Description
            Column
              Caption
              DataTypeName
              Description
              MetadataImage
            KPIComponent -> Measure (Column)
              Caption
              DataTypeName
              Description
            */

            private IADOTabularObject _function;
            private List<string> _sampleData;
            private bool _updatingBasicStats = false;
            private bool _updatingSampleData = false;
            private string _minValue = string.Empty;
            private string _maxValue = string.Empty;
            private long _distinctValues = 0;

            #region Constructors
            public TreeViewFunction(ADOTabularFunction function, GetChildrenDelegate getChildren, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane) : base(function, getChildren, options, eventAggregator, metadataPane)
            {
                _eventAggregator = eventAggregator;
                _sampleData = new List<string>();
                Function = function;
                Options = options;
                Description = function.Description;
                MetadataImage = function.MetadataImage;

            }

            //public TreeViewColumn(string displayFolder, GetChildrenDelegate getChildren, IGlobalOptions options, IEventAggregator eventAggregator) : base(getChildren, options, eventAggregator)
            //{
            //    _eventAggregator = eventAggregator;
            //    _sampleData = new List<string>();
            //    //Column = column;
            //    //_column = column;
            //    Options = options;

            //    MetadataImage = MetadataImages.Folder;

            //}

            //public TreeViewFunction(IADOTabularObjectReference reference, GetChildrenDelegate getChildren, ADOTabularTable table, IGlobalOptions options, IEventAggregator eventAggregator, IMetadataPane metadataPane)
            //    : base(table, getChildren, options, eventAggregator, metadataPane)
            //{
            //    Options = options;
            //    IADOTabularFolderReference folder = reference as IADOTabularFolderReference;
            //    if (folder == null)
            //    {
            //        Function = table.Columns.GetByPropertyRef(reference.InternalReference);
            //        MetadataImage = Column.GetMetadataImage();
            //    }
            //    else
            //    {
            //        _caption = reference.Name;
            //        MetadataImage = MetadataImages.Folder;
            //    }
            //}


            #endregion

            public MetadataImages MetadataImage { get; set; }

            private string _caption = string.Empty;
            public string Caption => Function?.Caption ?? _caption;
            public override string Name => Function?.Name ?? _caption;
            public override ADOTabularObjectType ObjectType => Function.ObjectType;
            public string Description { get; private set; }
            string IADOTabularObject.DaxName => Function?.DaxName ?? string.Empty;



            public IADOTabularObject Function { get; }

            public override bool IsVisible { get; }   // TODO - implement IsVisible

            public override bool IsCriteriaMatched(string criteria)
            {
                if (!this.MetadataPane.ShowHiddenObjects && !this.IsVisible) return false;
                return String.IsNullOrEmpty(criteria) || Caption.IndexOf(criteria, StringComparison.InvariantCultureIgnoreCase) >= 0;
            }

        }
    }
}
