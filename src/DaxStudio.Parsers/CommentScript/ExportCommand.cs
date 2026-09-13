using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class ExportCommand : ScriptCommand
    {
        public ExportCommand(ExportTarget target, string fileName = null)
        {
            Target = target;
            FileName = fileName;
        }

        public ExportCommand(TestReportFormat reportFormat, string fileName)
            : this(ExportTarget.TestResults, fileName)
        {
            ReportFormat = reportFormat;
        }

        public ExportTarget Target { get; }
        public string FileName { get; set; }
        public TestReportFormat? ReportFormat { get; }
    }
}
