using ADOTabular.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ADOTabular
{
    public class ADOTabularTable :IADOTabularObject, IADOTabularFolderReference
    {
        private readonly IADOTabularConnection _adoTabConn;
        private ADOTabularColumnCollection _columnColl;
        private ADOTabularMeasureCollection _measuresColl;        

       /* public ADOTabularTable(ADOTabularConnection adoTabConn, DataRow dr, ADOTabularModel model)
        {
            Caption = dr["DIMENSION_NAME"].ToString();
            IsVisible = bool.Parse(dr["DIMENSION_IS_VISIBLE"].ToString());
            Description = dr["DESCRIPTION"].ToString();
            _adoTabConn = adoTabConn;
            _model = model;
        }
        */
        public ADOTabularTable(IADOTabularConnection adoTabConn, ADOTabularModel model, string internalReference, string name, string caption, string description, bool isVisible, bool isPrivate, bool showAsVariationsOnly )
        {
            _adoTabConn = adoTabConn;
            InternalReference = internalReference;
            Name = name ?? internalReference;
            Caption = caption ?? name ?? internalReference;
            DaxName = GetDaxName();
            Description = description;
            IsVisible = isVisible;
            Relationships = new List<ADOTabularRelationship>();
            FolderItems = new List<IADOTabularObjectReference>();
            Private = isPrivate;
            ShowAsVariationsOnly = showAsVariationsOnly;
            Model = model;
        }

        public string DaxName
        {
            get;
            private set;
        }

        private string GetDaxName()
        {
            const string VALID_NAME_START = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_";
            const string STANDARD_NAME_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_0123456789";

            bool goodFirstCharacter = VALID_NAME_START.IndexOf(Name[0]) >= 0;
            bool noSpecialCharacters = !Name.Where((c) => STANDARD_NAME_CHARS.IndexOf(c) < 0).Any();
            //string nameUpper = Name.ToUpper();
            //bool noSpecialName = specialNames.Where((s) => s == nameUpper).Count() == 0;
            bool noSpecialName = !(_adoTabConn.Keywords.Contains(Name, StringComparer.OrdinalIgnoreCase) || _adoTabConn.AllFunctions.Contains(Name, StringComparer.OrdinalIgnoreCase));
            if (Name.Length > 0
                && goodFirstCharacter
                && noSpecialCharacters
                && noSpecialName)
            {
                return Name;
            }
            else
            {
                // need to double up any single quote characters
                return $"'{Name.Replace("'", "''")}'";
            }
        }

        public string InternalReference { get; private set; }

        public string Caption { get; private set; }
        public string Name { get; private set; }

        public string Description { get; private set; }

        public List<IADOTabularObjectReference> FolderItems { get; }

        public bool IsVisible { get; private set; }

        public ADOTabularColumnCollection Columns
        {
            get { return _columnColl ??= new ADOTabularColumnCollection(_adoTabConn, this); }
        }

        public int ColumnCount => Columns.Count(c =>
            c.Contents != "RowNumber" &&
            (c.ObjectType == ADOTabularObjectType.Column || 
             c.ObjectType == ADOTabularObjectType.Level));

        public int MeasureCount => Columns.Count(c =>
            c.Contents != "RowNumber" &&
            (c.ObjectType == ADOTabularObjectType.Measure ||
             c.ObjectType == ADOTabularObjectType.KPI     || 
             c.ObjectType == ADOTabularObjectType.KPIGoal ||
             c.ObjectType == ADOTabularObjectType.KPIStatus));

        public ADOTabularMeasureCollection Measures
        {
            get { return _measuresColl ??= new ADOTabularMeasureCollection(_adoTabConn, this); }
        }

        public ADOTabularModel Model { get; private set; }

        public MetadataImages MetadataImage
        {
            get { return IsVisible && !Private && !ShowAsVariationsOnly ? MetadataImages.Table : MetadataImages.HiddenTable; }
        }

        public IList<ADOTabularRelationship> Relationships { get; private set; } 
        public ADOTabularObjectType ObjectType => ADOTabularObjectType.Table;

        public FolderReferenceType ReferenceType => FolderReferenceType.None;

        public bool Private { get; }
        public bool ShowAsVariationsOnly { get; }

        public bool IsDateTable => DataCategory == "Time";
        public string DataCategory { get; internal set; }

        public long RowCount { get; private set; }

        public void UpdateBasicStats(ADOTabularConnection connection)
        {
            if (connection == null) return;

            string qry = $"{Constants.InternalQueryHeader}\nEVALUATE ROW(\"RowCount\", COUNTROWS({DaxName}) )";
            try
            {
                using var dt = connection.ExecuteDaxQueryDataTable(qry);
                long.TryParse(dt.Rows[0][0].ToString(), out var rows);
                RowCount = rows;
            }
            catch
            {
                // swallow any errors getting the row count
                RowCount = 0;
            }
            
            
        }

    }
}
