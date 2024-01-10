namespace DaxStudio.UI.Interfaces
{
    public interface IHaveLongRunningOperation
    {
        public bool IsRunning { get; }
        public void Cancel();
    }
}
