using Caliburn.Micro;

namespace DaxStudio.UI.Interfaces {
    public interface IHaveShutdownTask {
        IResult GetShutdownTask();
        string FileName { get; }
    }
}