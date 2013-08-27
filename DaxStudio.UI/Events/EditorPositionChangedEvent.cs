namespace DaxStudio.UI.Events
{
    public class EditorPositionChangedMessage
    {
        public EditorPositionChangedMessage(int column, int line)
        {
            Column = column;
            Line = line;
        }
        public int Column { get; set; }
        public int Line { get; set; }
    }
}
