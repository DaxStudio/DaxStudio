using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using System.Text;

namespace DaxStudio.UI.Utils
{
    public static class ParameterHelper
    {
        public static void WriteParameterXml(DocumentViewModel document, QueryInfo queryInfo)
        {
            var paramXml = GetParameterXml(queryInfo);
            document.AppendText(paramXml);
        }

        private static string GetParameterXml(QueryInfo queryInfo)
        {
            if (queryInfo.Parameters.Count == 0) return string.Empty;

            StringBuilder sbParams = new StringBuilder();
            // write parameters
            sbParams.Append("\n<Parameters xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns=\"urn:schemas-microsoft-com:xml-analysis\">");
            foreach (var p in queryInfo.Parameters)
            {
                sbParams.Append("  <Parameter>\n");
                sbParams.AppendFormat("    <Name>{0}</Name>\n", p.Value.Name);
                sbParams.AppendFormat("    <Value xsi:type=\"xsd: string\">{0}</Value>\n", p.Value.Value);
                sbParams.Append("  </Parameter>\n");
            }
            sbParams.Append("</Parameters>");
            return sbParams.ToString();
        }
    }
}
