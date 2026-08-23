using System;
using System.Data;
using System.Globalization;

namespace ADOTabular
{
    public class ADOTabularFunctionArgument
    {
        
        public ADOTabularFunctionArgument(DataRow dr)
        {
            if (dr == null) throw new ArgumentNullException(nameof(dr));
            Name = GetString(dr, "NAME");
            Description = GetString(dr, "DESCRIPTION");
            Optional = GetBool(dr, "OPTIONAL");
            // Note: the column name has historically been spelt inconsistently across providers
            // ("REPEATABLE" / "REPEATING") so we check both and default to false if neither is present.
            Repeatable = GetBool(dr, "REPEATABLE") || GetBool(dr, "REPEATING");
            RepeatGroup = HasColumn(dr, "REPEATGROUP") && int.TryParse(dr["REPEATGROUP"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rg)
                ? rg
                : 0;
        }

        private static bool HasColumn(DataRow dr, string columnName)
        {
            return dr.Table != null && dr.Table.Columns.Contains(columnName);
        }

        private static string GetString(DataRow dr, string columnName)
        {
            return HasColumn(dr, columnName) ? dr[columnName].ToString() : string.Empty;
        }

        private static bool GetBool(DataRow dr, string columnName)
        {
            return HasColumn(dr, columnName) && bool.TryParse(dr[columnName].ToString(), out var value) && value;
        }

        public ADOTabularFunctionArgument(string name, string description, bool optional, bool repeatable, int repeatGroup)
        {
            Name = name;
            Description = description;
            Optional = optional;
            Repeatable = repeatable;
            RepeatGroup = repeatGroup;
        }

        public string Name { get; private set; }
        public string Description { get; private set; }
        public bool Optional { get; private set; }
        public bool Repeatable { get; private set; }
        public int RepeatGroup { get; private set; }
    }
}
