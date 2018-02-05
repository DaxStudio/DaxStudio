using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using Serilog;

namespace DaxStudio.UI.Utils
{
    public static class DaxHelper
    {
        // detects a parameter
        const string paramRegex = @"@[^\[\]\s]*\b+(?![^\[]*\])";
        
        public static string PreProcessQuery(string query, IEventAggregator eventAggregator)
        {
	        var lines = query.Split('\n');
	        var inParams = false;
	        var sbParams = new StringBuilder();
	        var sbQuery = new StringBuilder();
	        foreach (var line in lines)
	        {
		        if (line.Trim().StartsWith("<Parameters"))
		        {
			        inParams = true;
		        }
		        
		        if (inParams)
		        {
			        sbParams.Append(line);
		        }
		        else
		        {
			        sbQuery.Append(line);
		        }

                if (line.Trim().EndsWith("</Parameters>"))
                {
                    inParams = false;
                }
            }

		    if (sbParams.Length > 0)
		    {
			    var paramDictionary = ParseParams(sbParams.ToString(), eventAggregator);
                return replaceParamsInQuery(sbQuery, paramDictionary);
		    }
            return query;
        }

        public static Dictionary<string,string> ParseParams(string paramString, IEventAggregator eventAggregator)
        {
            
            var d = new Dictionary<string, string>();
            System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
            try
            {
                doc.LoadXml(paramString);
                var nsMgr = new System.Xml.XmlNamespaceManager(doc.NameTable);
                nsMgr.AddNamespace("x", "urn:schemas-microsoft-com:xml-analysis");
                foreach (System.Xml.XmlNode n in doc.SelectNodes("/x:Parameters/x:Parameter", nsMgr))
                {
                    d.Add(n["Name"].InnerText, n["Value"].InnerText);
                }

                // if we did not find the proper namespace try searching for just the raw names
                if (d.Count == 0)
                {
                    foreach (System.Xml.XmlNode n in doc.SelectNodes("/Parameters/Parameter", nsMgr))
                    {
                        d.Add(n["Name"].InnerText, n["Value"].InnerText);
                    }

                }
            } catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} Error merging query parameters", "DaxHelper", "ParseParams");
                eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, "The Following Error occurred while trying to parse a parameter block: " + ex.Message));
            }
            return d;
            
        }

        public static string replaceParamsInQuery(StringBuilder query, Dictionary<string,string> param)
        {
            
            var rexStart = "@";//(?<=@)";
            var rexEnd = "(?=[\\s|,|\\)|$])";
            //var rex = new System.Text.RegularExpressions.Regex("(?<=@)\\w*");


            var sqry = query.ToString() + " "; // HACK: adding space as parameters at the end of the string were not being matched
            foreach(var p in param.Keys)
            {
                sqry = new Regex(string.Format("{0}{1}{2}", rexStart, p, rexEnd), RegexOptions.Singleline| RegexOptions.IgnoreCase)
                    .Replace(sqry,"\"" + param[p] + "\"");
                //query.Replace(string.Format("@{0}",p), string.Format("\"{0}\"", param[p]));
            }
             
            //return query.ToString().Trim();
            return sqry.TrimEnd();
        }
    }
}
