namespace DaxStudio.UI.Events
{
    public class FileSavedEvent
    {
        public FileSavedEvent(string fileName) {
            FileName = fileName;
        }

        public string FileName { get; }

    }
}
