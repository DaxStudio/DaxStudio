namespace DaxStudio.UI.Events
{
    public class FileOpenedEvent
    {
        public FileOpenedEvent(string fileName)
        {
            FileName = fileName;
        }

        public string FileName { get; private set; }
    }
}
