using ADOTabular.Interfaces;

namespace DaxStudio.UI.Events
{
    public class SendTabularObjectToEditor
    {
        public SendTabularObjectToEditor( IADOTabularObject item)
        {
            TabularObject = item;
        }

        public IADOTabularObject TabularObject { get; }
    }
}
