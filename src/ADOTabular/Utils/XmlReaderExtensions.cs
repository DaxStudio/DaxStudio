using System.Xml;

namespace ADOTabular.Utils
{
    public static class XmlReaderExtensions
    {
        public static void ReadToNextElement(this XmlReader reader)
        {
            while (reader.NodeType != XmlNodeType.Element && reader.NodeType != XmlNodeType.EndElement) {
                reader.Read();
            }
        }
    }
}
