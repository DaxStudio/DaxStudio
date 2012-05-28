namespace DaxStudio
{
    public interface IOutputWindow
    {
        void ClearOutput();
        void WriteOutputMessage(string message);
        void WriteOutputError(string errorMessage);
    }
}
