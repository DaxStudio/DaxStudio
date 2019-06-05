using DaxStudio.UI.Model;
using System.IO;
using System.Xml;

namespace DaxStudio.UI.Extensions
{
    public static class QueryBeginEventExtensions
    {
        public static string ParseEffectiveUsername(this QueryBeginEvent beginEvent)
        {
            XmlDocument xd = new XmlDocument();
            xd.LoadXml(beginEvent.RequestProperties);
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xd.NameTable);
            nsmgr.AddNamespace("x", xd.DocumentElement.NamespaceURI);
            var effNode = xd.SelectSingleNode("x:PropertyList/x:EffectiveUserName",nsmgr);
            var effName = effNode?.InnerText??string.Empty;
            return effName;
        }
    }
}
