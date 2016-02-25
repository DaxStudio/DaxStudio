using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular.DatabaseProfile
{
    public static class DatabaseProfiler
    {
        public static DatabaseProfile Create(ADOTabularDatabase database)
        {

            DatabaseProfile profile = new DatabaseProfile()
            {
                Name = database.Name,
                Id = database.Id,
                ProfileDate = DateTime.Now
            };

            ProcessCatalog(profile, database);
            ProcessTables(profile, database);
            ProcessColumns(profile, database);
            ProcessColumnSegments(profile, database);
            ProcessColumnCardinality(profile, database);

            return profile;
        }

        private static void ProcessColumnCardinality(DatabaseProfile profile, ADOTabularDatabase database)
        {
            var tbl = GetColumnCardinality(database);
            foreach (DataRow dr in tbl.Rows)
            {
                var tab = profile.Tables[dr["TABLE_NAME"].ToString()];
                var hierId = dr["COLUMN_HIERARCHY_ID"].ToString();
                var colId = hierId.Substring(hierId.IndexOf('$',3) + 1);

                var col = tab.Columns.GetById(colId);
                col.Cardinality = dr.Field<long>("COLUMN_CARDINALITY");
            }
        }


        private static void ProcessCatalog(DatabaseProfile profile, ADOTabularDatabase database)
        {
            var tbl = GetCatalog(database);
            DataRow dr = tbl.Rows[0];
            profile.ModifiedDate = dr.Field<DateTime>("DATE_MODIFIED");
        }


        private static void ProcessColumnSegments(DatabaseProfile profile, ADOTabularDatabase database)
        {
            var tbl = GetColumnSegments(database);
            foreach (DataRow dr in tbl.Rows)
            {
                var tab = profile.Tables[dr["TABLE_NAME"].ToString()];
                Column col = tab.Columns.GetById(dr["COLUMN_ID"].ToString()); // may not be found...
                
                var tableId = dr["TABLE_ID"].ToString();

                switch (tableId.Substring(0,2))
                {
                    case "H$": {
                        var colId = tableId.Substring(tableId.LastIndexOf('$') + 1);
                        tab.Columns.GetById(colId).HierarchySegments.Add(CreateSegment(dr,SegmentType.Hierarchy));
                        break;
                    }
                    case "R$":{
                        tab.RelationshipSegments.Add(CreateSegment(dr, SegmentType.Relationship));
                        break;
                    }
                    case "U$":{
                        var colId = tableId.Substring(tableId.LastIndexOf('$') + 1);
                        tab.UserHierarchies[colId].Segments.Add(CreateSegment(dr, SegmentType.UserHierarchy)); // todo - group segments from the same hierarchy together
                        break;
                    }
                    default: {
                        col.DataSegments.Add(CreateSegment(dr));
                        break;
                    }
                }
            }
        }

        private static Segment CreateSegment(DataRow dr)
        {
            return CreateSegment(dr, SegmentType.Data);
        }

        private static Segment CreateSegment(DataRow dr, SegmentType segmentType)
        {
            var seg = new Segment
            {
                SegmentIndex = dr.Field<long>("SEGMENT_NUMBER"),
                PartitionIndex = dr.Field<long>("TABLE_PARTITION_NUMBER"),
                PartitionName = dr["PARTITION_NAME"].ToString(),
                RowCount = dr.Field<long>("SEGMENT_ROWS"),
                UsedSize = dr.Field<ulong>("USED_SIZE"),
                CompressionType = dr["COMPRESSION_TYPE"].ToString(),
                BitsCount = dr.Field<long>("BITS_COUNT"),
                BookmarkBitsCount = dr.Field<long>("BOOKMARK_BITS_COUNT"),
                VertipaqState = dr["VERTIPAQ_STATE"].ToString(),
                SegmentType = (segmentType == SegmentType.Hierarchy || segmentType == SegmentType.UserHierarchy)? dr["COLUMN_ID"].ToString():null
            };
            
            return seg;
        }

        private static void ProcessColumns(DatabaseProfile profile, ADOTabularDatabase database)
        {
            var tbl = GetColumns(database);
            foreach (DataRow dr in tbl.Rows)
            {
                profile.Tables[dr["TABLE_NAME"].ToString()].Columns.Add(new Column() { 
                    Name = dr["COLUMN_NAME"].ToString(), 
                    Id = dr["COLUMN_ID"].ToString(), 
                    DataType = dr["DATATYPE"].ToString(),
                    DictionarySize = dr.Field<ulong>("DICTIONARY_SIZE_BYTES") });
            }
        }

        private static void ProcessTables(DatabaseProfile profile, ADOTabularDatabase database)
        {
            var tbl = GetTables(database);
            foreach (DataRow dr in tbl.Rows)
            {
                profile.Tables.Add(new Table() { Name = dr["TABLE_NAME"].ToString(), 
                    Id = dr["TABLE_ID"].ToString(), 
                    RowCount = dr.Field<long>("ROWS_IN_TABLE"),
                    RiViolationCount = dr.Field<long>("RIVIOLATION_COUNT")
                });
            }

        }

        private static DataTable GetColumnSegments(ADOTabularDatabase database)
        {
            var cnn = database.Connection;
            var tbl = cnn.ExecuteDaxQueryDataTable(Queries.Constants.ColumnSegments);
            return tbl;
        }

        private static DataTable GetColumnCardinality(ADOTabularDatabase database)
        {
            var cnn = database.Connection;
            var tbl = cnn.ExecuteDaxQueryDataTable(Queries.Constants.ColumnCardinality);
            return tbl;
        }

        private static DataTable GetColumns(ADOTabularDatabase database)
        {
            var cnn = database.Connection;
            var tbl = cnn.ExecuteDaxQueryDataTable(Queries.Constants.Columns);
            return tbl;
        }

        private static DataTable GetTables(ADOTabularDatabase database)
        {
            var cnn = database.Connection;
            var tbl = cnn.ExecuteDaxQueryDataTable(Queries.Constants.Tables);
            return tbl;
        }

        private static DataTable GetCatalog(ADOTabularDatabase database)
        {
            var cnn = database.Connection;
            var tbl = cnn.ExecuteDaxQueryDataTable(string.Format("SELECT * FROM $SYSTEM.DBSCHEMA_CATALOGS WHERE [CATALOG_NAME] = '{0}'", database.Name));
            return tbl;
        }
    }
}
