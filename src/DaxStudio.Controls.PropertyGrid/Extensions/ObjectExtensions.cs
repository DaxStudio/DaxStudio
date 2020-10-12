using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Controls.PropertyGrid
{
    public static class ObjectExtensions
    {
        public static Type As<Type>(this object source) => source is Type ? (Type)source : default(Type);
    }
}
