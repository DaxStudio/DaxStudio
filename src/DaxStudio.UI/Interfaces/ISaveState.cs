using System.IO.Packaging;

namespace DaxStudio.UI.Interfaces
{
    public interface ISaveState
    {
        void Save(string filename);
        void Load(string filename);

        void SavePackage(Package package);
        void LoadPackage(Package package);
    }
}
