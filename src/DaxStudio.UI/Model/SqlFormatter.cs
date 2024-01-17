using DaxStudio.UI.Interfaces;
using PoorMansTSqlFormatterLib.Interfaces;
using PoorMansTSqlFormatterLib.ParseStructure;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;

namespace DaxStudio.UI.Model
{

    internal static class SqlFormatter 
    {
        #region ISqlFormatProvider

        // SQL Formatter for DirectQuery
        private static ISqlTokenizer _tokenizer = new PoorMansTSqlFormatterLib.Tokenizers.TSqlStandardTokenizer();
        private static ISqlTokenParser _parser = new PoorMansTSqlFormatterLib.Parsers.TSqlStandardParser();
        private static ISqlTreeFormatter _formatter = new PoorMansTSqlFormatterLib.Formatters.TSqlDaxStudioFormatter(new PoorMansTSqlFormatterLib.Formatters.TSqlStandardFormatterOptions
        {
            BreakBeforeJoin = true,
            BreakJoinOnSections = false,
            ExpandBetweenConditions = true,
            ExpandBooleanExpressions = true,
            ExpandCaseStatements = true,
            ExpandCommaLists = true,
            ExpandInLists = true,
            HTMLColoring = false,
            IndentString = "\t", // 4 spaces to get a good indentation on tooltips
            KeywordStandardization = true,
            MaxLineWidth = 160,
            NewClauseLineBreaks = 1,
            NewStatementLineBreaks = 1,
            SpaceAfterExpandedComma = true,
            SpacesPerTab = 4,
            TrailingCommas = true,
            UppercaseKeywords = true
        });

        static Regex searchColumnNames = new Regex(@"c\d+", RegexOptions.Compiled);
        //static Regex searchTableNames = new Regex(@"t\d+", RegexOptions.Compiled);
        /// <summary>
        /// Rename column aliases created by DirectQuery SQL by using meaningful column names
        /// </summary>
        /// <param name="node">Root node of the tree to scan</param>
        /// <returns>Node with replaced structure</returns>
        private static Node RenameSqlColumnAliases(Node node)
        {
            Dictionary<string, string> aliases = new Dictionary<string, string>();

            ScanNode(node);
            ReplaceAliases(node);
            return node;

            void ScanNode(Node node)
            {
                if (node.Name == SqlStructureConstants.ENAME_BRACKET_QUOTED_NAME
                        && !string.IsNullOrEmpty(node?.TextValue))
                {
                    if (searchColumnNames.Match(node.TextValue).Value == node.TextValue)
                    {
                        // Check if parent has AS definition and store it if found
                        string definition = GetParentColumnDefinition(node, node.Parent.Children);
                        if (definition != null)
                        {
                            // If the node already exists, there is a difference in the alias but the column name is the same.
                            // The alias c?? should correspond to the column position in model metadata - we should evaluate a safer way to identify
                            // the original column name in the data source
                            if (!aliases.ContainsKey(node.TextValue))
                            {
                                aliases.Add(node.TextValue, definition);
                            }
                        }
                    }

                    // The table alias cannot be resolved without a refactor of the tree that removes
                    // the internal $Table references and searches for the table aliases in the 
                    // node tree after it has been rebuilt and simplified

                    //else if (searchTableNames.Match(node.TextValue).Value == node.TextValue)
                    //{
                    //    // Check if parent has AS definition and store it if found
                    //    string definition = GetParentTableDefinition(node, node.Parent.Children);
                    //    if (definition != null)
                    //    {
                    //        aliases.Add(node.TextValue, definition);
                    //    }
                    //}
                }
                ScanNodeList(node.Children);
            }

            void ScanNodeList(IEnumerable<Node> listNodes)
            {
                foreach (Node contentElement in listNodes)
                {
                    ScanNode(contentElement);
                }
            }

            void ReplaceAliases(Node node)
            {
                if (node.Name == SqlStructureConstants.ENAME_BRACKET_QUOTED_NAME
                    && !string.IsNullOrEmpty(node?.TextValue)
                    && (aliases.TryGetValue(node.TextValue, out string description))
                    )
                {
                    node.TextValue = description;
                }
                ReplaceNodeList(node.Children);
            }

            void ReplaceNodeList(IEnumerable<Node> listNodes)
            {
                foreach (Node contentElement in listNodes)
                {
                    ReplaceAliases(contentElement);
                }
            }

            string GetParentColumnDefinition(Node node, IEnumerable<Node> listNodes)
            {
                var step1 = listNodes.TakeWhile(n => n != node);
                var lastAs = step1.LastOrDefault(n => n.Name == SqlStructureConstants.ENAME_OTHERKEYWORD && n.TextValue.ToUpperInvariant() == "AS");
                var step2 = step1.TakeWhile(n => n != lastAs);
                var step3 = step2.Where(n => n.Name != SqlStructureConstants.ENAME_WHITESPACE);
                var step4 = step3.ToArray();
                var previousNodes = step4;
                if (previousNodes.Length >= 3
                    && previousNodes[previousNodes.Length - 3].Name == SqlStructureConstants.ENAME_BRACKET_QUOTED_NAME
                    && previousNodes[previousNodes.Length - 2].Name == SqlStructureConstants.ENAME_PERIOD
                    && previousNodes[previousNodes.Length - 1].Name == SqlStructureConstants.ENAME_BRACKET_QUOTED_NAME
                    )
                {
                    return $"{previousNodes[previousNodes.Length - 3].TextValue}_{previousNodes[previousNodes.Length - 1].TextValue}";
                }
                else
                {
                    return null;
                }
            }

#pragma warning disable CS8321 // Local function is declared but never used
            string GetParentTableDefinition(Node node, IEnumerable<Node> listNodes)
            {
                var step1 = listNodes.TakeWhile(n => n != node);
                var lastAs = step1.LastOrDefault(n => n.Name == SqlStructureConstants.ENAME_OTHERKEYWORD && n.TextValue.ToUpperInvariant() == "AS");
                var step2 = step1.TakeWhile(n => n != lastAs);
                var step3 = step2.Where(n => n.Name != SqlStructureConstants.ENAME_WHITESPACE);
                var step4 = step3.ToArray();
                var previousNodes = step4;
                if (previousNodes.Length >= 3
                    && previousNodes[previousNodes.Length - 3].Name == SqlStructureConstants.ENAME_BRACKET_QUOTED_NAME
                    && previousNodes[previousNodes.Length - 2].Name == SqlStructureConstants.ENAME_PERIOD
                    && previousNodes[previousNodes.Length - 1].Name == SqlStructureConstants.ENAME_BRACKET_QUOTED_NAME
                    )
                {
                    return $"{previousNodes[previousNodes.Length - 3].TextValue}_{previousNodes[previousNodes.Length - 1].TextValue}";
                }
                else
                {
                    return null;
                }
            }
#pragma warning restore CS8321 // Local function is declared but never used
        }

        public static string FormatSql(string textData)
        {
            var tokenizedSql = _tokenizer.TokenizeSQL(textData);
            var parsedSql = _parser.ParseSQL(tokenizedSql);
            var renamedSql = RenameSqlColumnAliases(parsedSql);
            string text = _formatter.FormatSQLTree(renamedSql);
            return text;
        }
        #endregion
    }
}
