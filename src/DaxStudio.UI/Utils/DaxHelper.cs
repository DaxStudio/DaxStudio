using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using Serilog;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.Utils
{
    public static class DaxHelper
    {
        // detects a parameter
        //const string paramRegex = @"(?:@)(?<name>[^\[\]\s,=]*\b+(?![^\[]*\]))";
        //const string paramRegex = @"\[[^\]]*\](?# ignore column names)|'[^']*'(?# ignore tables names)|""[^""]*""(?# ignore strings)|(?:@)(?<name>[^\[\]\);\s,=]*\b+(?![^\[]*\]))";
        const string paramRegex = @"\[[^\]]*\](?# ignore column names)|'[^']*'(?# ignore tables names)|""[^""]*""(?# ignore strings)|(?:@)(?<name>[^\[\]\);\s,=]*\b+)";
        const string commentRegex = @"\/\*(\*(?!\/)|[^*])*\*\/|(//.*)|(--.*)";
        private static Regex rexComments = new Regex(commentRegex, RegexOptions.Compiled | RegexOptions.Multiline);
        private static Regex rexParams = new Regex(paramRegex, RegexOptions.Compiled);

        const string startRegex = "@";//(?<=@)";
        const string endRegex = @"(?=[\s,\),;,\,,//,--,\\\\}]|$)";
        
        public static void PreProcessQuery(QueryInfo queryInfo, string query, IEventAggregator eventAggregator)
        {
	        var lines = query.Split('\n');
	        var inParams = false;
	        var sbParams = new StringBuilder();
	        var sbQuery = new StringBuilder();
	        foreach (var line in lines)
	        {
		        if (line.Trim().StartsWith("<Parameters")) inParams = true;

                if (inParams)
                    sbParams.Append(line);
                else {
                    sbQuery.Append(line);
                    sbQuery.Append('\n');
                }

                if (line.Trim().EndsWith("</Parameters>")) inParams = false;

            }
            string qry = sbQuery.ToString();
            PopulateParameters(qry,  queryInfo.Parameters);

            queryInfo.NeedsParameterValues = queryInfo.Parameters.Count > 0 && sbParams.Length == 0;

		    if (sbParams.Length > 0)
		    {
            //    queryInfo.NeedsParameterValues = false;
			    ParseParams(sbParams.ToString(), queryInfo.Parameters, eventAggregator);
                queryInfo.QueryText = sbQuery.ToString();

            }
            else
                queryInfo.QueryText = query;
            
        }

        public static void ParseParams(string paramString, Dictionary<string, QueryParameter> paramDict, IEventAggregator eventAggregator)
        {
            bool foundXmlNameSpacedParams = false;
            System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
            try
            {
                doc.LoadXml(paramString);
                var nsMgr = new System.Xml.XmlNamespaceManager(doc.NameTable);
                nsMgr.AddNamespace("x", "urn:schemas-microsoft-com:xml-analysis");
                foreach (System.Xml.XmlNode n in doc.SelectNodes("/x:Parameters/x:Parameter", nsMgr))
                {
                    foundXmlNameSpacedParams = true;
                    string paramTypeName = n["Value"].Attributes["xsi:type"].Value;
                    Type paramType = DaxStudio.Common.XmlTypeMapper.GetSystemType(paramTypeName);
                    object val = Convert.ChangeType(n["Value"].InnerText, paramType);
                    if (!paramDict.ContainsKey(n["Name"].InnerText))
                        paramDict.Add(n["Name"].InnerText, new QueryParameter(n["Name"].InnerText, val, paramTypeName));
                    else
                        paramDict[n["Name"].InnerText] = new QueryParameter(n["Name"].InnerText, val, paramTypeName);
                }

                // if we did not find the proper namespace try searching for just the raw names
                if (!foundXmlNameSpacedParams)
                {
                    foreach (System.Xml.XmlNode n in doc.SelectNodes("/Parameters/Parameter", nsMgr))
                    {
                        string paramTypeName = "xsd:string"; 
                        if (n["Value"].Attributes.Count > 0)
                        { 
                            if (n["Value"].Attributes["xsi:type"] != null)
                            {
                                paramTypeName = n["Value"].Attributes["xsi:type"].Value;
                            }
                        
                        }
                        

                        if (!paramDict.ContainsKey(n["Name"].InnerText))
                            paramDict.Add(n["Name"].InnerText, new QueryParameter(n["Name"].InnerText, n["Value"].InnerText, paramTypeName));
                        else
                            paramDict[n["Name"].InnerText] = new QueryParameter(n["Name"].InnerText, n["Value"].InnerText, paramTypeName);
                    }

                }
            } catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} Error merging query parameters", "DaxHelper", "ParseParams");
                eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, "The Following Error occurred while trying to parse a parameter block: " + ex.Message));
            }
            
        }

        public static string replaceParamsInQuery(string query, Dictionary<string,QueryParameter> param)
        {
            
            //var rex = new System.Text.RegularExpressions.Regex("(?<=@)\\w*");

            var sqry = query + " "; // HACK: adding space as parameters at the end of the string were not being matched
            foreach(var p in param.Keys)
            {
                var quotes = string.Empty;

                if (param[p].Value is string) quotes = "\"";
                sqry = new Regex(string.Format("{0}{1}{2}", startRegex, p, endRegex), RegexOptions.Singleline| RegexOptions.IgnoreCase)
                    .Replace(sqry, param[p].LiteralValue);
                //query.Replace(string.Format("@{0}",p), string.Format("\"{0}\"", param[p]));
            }
             
            //return query.ToString().Trim();
            return sqry.TrimEnd();
        }

        public static void PopulateParameters(string qry,  Dictionary<string,QueryParameter> paramDict)
        {
            // strip out comments before looking for parameters
            var cleanQry = rexComments.Replace(qry, "");
            var matches = rexParams.Matches(cleanQry);
            foreach(Match m in matches)
            {
                // if we have no "name" group this means the @ is between quotes in a string
                // so go to the next match
                if (string.IsNullOrEmpty(m.Groups["name"].Value)) continue; 

                if (!paramDict.ContainsKey(m.Groups["name"].Value)) paramDict.Add(m.Groups["name"].Value, new QueryParameter(m.Groups["name"].Value));
            }

        }

        // returns a dax column name without the single quotes around the table name
        public static string GetDaxResultColumnName(string columnName, ADOTabular.ADOTabularConnection connection)
        {
            var parts = columnName.Split('[');
            if (parts.Length != 2) return columnName;

            if (!connection.Database.Models[0].Tables.TryGetValue(parts[0], out var table)) return columnName;


            var tableName = table.DaxName;
            return $"{tableName}[{parts[1]}";
        }

        // returns a dax column name without the single quotes around the table name
        public static string GetQuotedColumnName(string columnName)
        {
            var parts = columnName.Split('[');
            if (parts.Length != 2) return columnName;
            var tableName = parts[0];
            if (tableName.StartsWith("'")) return $"{tableName}[{parts[1]}";

            return $"'{tableName}'[{parts[1]}";
        }
    }
}
