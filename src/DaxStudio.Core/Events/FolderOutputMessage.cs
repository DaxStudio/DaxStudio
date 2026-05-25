namespace DaxStudio.Core.Events
{
    public class FolderOutputMessage : OutputMessage
    {
        public FolderOutputMessage(string text, string folder) : base(MessageType.Information, text)
        {
            FolderPath = folder;
        }
        public string FolderPath { get; private set; }
    }
}
