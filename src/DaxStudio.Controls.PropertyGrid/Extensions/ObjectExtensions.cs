
namespace DaxStudio.Controls.PropertyGrid
{
    public static class ObjectExtensions
    {
        public static TType As<TType>(this object source) => source is TType ? (TType)source : default(TType);
    }
}
