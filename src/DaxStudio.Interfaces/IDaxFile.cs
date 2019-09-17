namespace DaxStudio.Interfaces
{
    public interface IDaxFile
    {
        bool Pinned { get; set; }
        string FullPath { get; set; }
    }
}