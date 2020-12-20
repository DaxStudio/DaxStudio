using System;

namespace ADOTabular.MetadataInfo {
    public class SsasVersion {
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public string SSAS_VERSION { get; set; }

        public string PRODUCT_TYPE { get; set; }
        public string PRODUCT_NAME { get; set; }

        public DateTime? RELEASE_DATE { get; set; }

        public string PRODUCT_VERSION_SHORT { get; set; }
        public string PRODUCT_VERSION_LONG { get; set; }
        public DateTime CAPTURE_DATE { get; set; }
#pragma warning restore CA1707 // Identifiers should not contain underscores
    }
}
