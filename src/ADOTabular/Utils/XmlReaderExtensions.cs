using System.Diagnostics.Contracts;
using System.Xml;

namespace ADOTabular.Utils
{
    public static class XmlReaderExtensions
    {
        public static void ReadToNextElement(this XmlReader reader)
        {
            Contract.Requires(reader != null, "The reader parameter must not be null");
            while (reader.NodeType != XmlNodeType.Element && reader.NodeType != XmlNodeType.EndElement) {
                reader.Read();
            }
        }
    }
}
