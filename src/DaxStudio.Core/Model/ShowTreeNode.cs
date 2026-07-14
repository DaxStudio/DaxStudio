using System;
using System.Collections.Generic;

namespace DaxStudio.Core.Model
{
    /// <summary>
    /// A WPF-free node used to feed the tree-grid shown in the Results pane by the
    /// Comment Script <c>--&gt; SHOW</c> commands (DEPENDENCIES / LAST_UPDATED / MAX_UPDATED).
    /// Lives in Core so the connection layer can build the tree without referencing the UI;
    /// the tree-grid binds directly to these nodes via <c>Data.*</c> and <c>Children</c>.
    /// </summary>
    public class ShowTreeNode
    {
        public ShowTreeNode(string name, string objectType, string tableName = null, DateTime? lastModifiedUtc = null)
        {
            Name = name;
            ObjectType = objectType;
            TableName = tableName;
            LastModifiedUtc = lastModifiedUtc;
        }

        /// <summary>The object name (measure, column, table or function name).</summary>
        public string Name { get; set; }

        /// <summary>The object type (MEASURE, COLUMN, TABLE, FUNCTION, PARTITION, ...).</summary>
        public string ObjectType { get; set; }

        /// <summary>The owning table name, when applicable (blank for tables / functions).</summary>
        public string TableName { get; set; }

        /// <summary>The last schema-modified (or refreshed) time in UTC, when known.</summary>
        public DateTime? LastModifiedUtc { get; set; }

        /// <summary>Local-time display string for <see cref="LastModifiedUtc"/> (blank when null).</summary>
        public string LastModifiedDisplay =>
            LastModifiedUtc.HasValue ? LastModifiedUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : string.Empty;

        /// <summary>Child nodes. Bound as the tree-grid ChildrenBindingPath.</summary>
        public List<ShowTreeNode> Children { get; } = new List<ShowTreeNode>();
    }
}
