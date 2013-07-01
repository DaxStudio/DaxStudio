namespace DaxStudio.UI.Events
{
    public class LoadFileEvent
    {
        public LoadFileEvent(string fileName)
        {
            FileName = fileName;
        }

        public string FileName { get; set; }
    }
}
