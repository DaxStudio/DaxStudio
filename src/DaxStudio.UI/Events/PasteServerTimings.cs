namespace DaxStudio.UI.Events
{
    public class PasteServerTimingsEvent
    {
        /// <summary>
        /// Text header for copy/past of server timings information in DAX Studio editor
        /// </summary>
        public const string SERVERTIMINGS_HEADER = @"--     Total         FE         SE      SE CPU   Par.";

        public PasteServerTimingsEvent(bool includeHeader, string textResult )
        {
            IncludeHeader = includeHeader;
            TextResult = textResult;
        }

        public bool IncludeHeader { get; set; }

        public string TextResult { get; set; }
    }
}
