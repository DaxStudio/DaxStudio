using System;
using System.IO;
using System.Xml;

namespace ADOTabular
{
    public class ADOTabularServerProperties
    {
        private readonly Func<string> _discoverMetadata;
        private readonly object _metadataLock = new object();
        private XmlDocument _metadata;

        public ADOTabularServerProperties(Func<string> discoverMetadata)
        {
            _discoverMetadata = discoverMetadata ?? throw new ArgumentNullException(nameof(discoverMetadata));
        }

        public string Edition => GetValue("//d:Edition", string.Empty);
        public string Location => GetValue("//ddl400:ServerLocation", string.Empty);
        public string Mode => GetValue("//*[local-name()='ServerMode']", "Unknown");
        public string Id => GetValue("//*[local-name()='ID']", "Unknown");

        private string GetValue(string path, string fallback)
        {
            lock (_metadataLock)
            {
                if (_metadata == null)
                {
                    var metadata = new XmlDocument { XmlResolver = null };
                    using (var text = new StringReader(_discoverMetadata()))
                    using (var reader = XmlReader.Create(text,
                        new XmlReaderSettings { XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit }))
                    {
                        metadata.Load(reader);
                    }
                    _metadata = metadata;
                }

                var namespaces = new XmlNamespaceManager(_metadata.NameTable);
                namespaces.AddNamespace("d", "http://schemas.microsoft.com/analysisservices/2003/engine");
                namespaces.AddNamespace("ddl400", "http://schemas.microsoft.com/analysisservices/2012/engine/400");
                return _metadata.SelectSingleNode(path, namespaces)?.InnerText ?? fallback;
            }
        }
    }
}