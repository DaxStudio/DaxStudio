namespace DaxStudio.UI.Events
{
    public class PasteDaxFileEvent
    {
        public PasteDaxFileEvent(string fileName)
        {
            FileName = fileName;
        }
        public string FileName { get; private set; }
    }
}
