using Caliburn.Micro;

namespace DaxStudio.UI.Utils {
    public interface IHaveShutdownTask {
        IResult GetShutdownTask();
    }
}