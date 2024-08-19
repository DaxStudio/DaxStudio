namespace DaxStudio.Interfaces
{
    public interface ISaveable
    {
        string FileName { get; set; }
        bool IsDirty { get; set; }
        void Save();
        string DisplayName { get; set; }
        
        bool ShouldSave { get; set; }
            
    }
}
