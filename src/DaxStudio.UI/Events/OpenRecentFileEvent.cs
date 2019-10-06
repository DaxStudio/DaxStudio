
namespace DaxStudio.UI.Events
{
    public class OpenDaxFileEvent
    {
        public OpenDaxFileEvent(string fileName)
        {
            FileName = fileName;
        }
        public string FileName { get; private set; }
    }
}
