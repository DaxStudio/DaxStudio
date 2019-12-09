using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DaxStudio.UI.Interfaces
{
    public interface IAutoSaver
    {
        void CleanUpRecoveredFiles();
        void EnsureDirectoryExists(string file);
        string GetAutoSaveText(Guid autoSaveId);
        Dictionary<int, AutoSaveIndex> LoadAutoSaveMasterIndex();
        Task Save(DocumentTabViewModel tabs);
        void RemoveAll();

    }
}
