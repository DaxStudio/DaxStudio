using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace DaxStudio.UI.Utils
{
    public static class DaxHelper
    {
        //TODO - detects a parameter
        const string paramRegex = @"@[^\[\]\s]*\b+(?![^\[]*\])";
        
        public static string PreProcessQuery(string query)
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
			    var paramDictionary = ParseParams(sbParams.ToString());
                return replaceParamsInQuery(sbQuery, paramDictionary);
		    }
            return query;
        }

        public static Dictionary<string,string> ParseParams(string paramString)
        {
            var d = new Dictionary<string,string>();
            System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
            doc.LoadXml(paramString);
            var nsMgr = new System.Xml.XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("x", "urn:schemas-microsoft-com:xml-analysis");
            foreach( System.Xml.XmlNode n in doc.SelectNodes("/x:Parameters/x:Parameter",nsMgr))
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
