
namespace DaxStudio.UI.Events
{
    public class OpenRecentFileEvent
    {
        public OpenRecentFileEvent(string fileName)
        {
            FileName = fileName;
        }
        public string FileName { get; private set; }
    }
}
