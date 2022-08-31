using System.Threading.Tasks;

namespace DaxStudio.UI.Interfaces {
    public interface IDocumentWorkspace  {
        Task ActivateAsync(object document);
    }
}