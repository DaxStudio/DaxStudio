namespace ADOTabular.MetadataInfo {
    public class DaxFunction {
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public string SSAS_VERSION { get; set; }


        public string FUNCTION_NAME { get; set; }

        public string DESCRIPTION { get; set; }

        public string PARAMETER_LIST { get; set; }

        public int? RETURN_TYPE { get; set; }

        public int? ORIGIN { get; set; }

        public string INTERFACE_NAME { get; set; }

        public string LIBRARY_NAME { get; set; }

        public string DLL_NAME { get; set; }

        public string HELP_FILE { get; set; }

        public int? HELP_CONTEXT { get; set; }

        public string OBJECT { get; set; }

        public string CAPTION { get; set; }

        public string PARAMETERINFO { get; set; }

        public int? DIRECTQUERY_PUSHABLE { get; set; }
#pragma warning restore CA1707 // Identifiers should not contain underscores
    }
}
