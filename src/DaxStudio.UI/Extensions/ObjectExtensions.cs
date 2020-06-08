using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Navigation;

namespace DaxStudio.UI.Extensions
{
    public static class ObjectExtensions
    {
        public static T ToObject<T>(this IDictionary<string, object> source)
            where T : class, new()
        {
            var someObject = new T();
            var someObjectType = someObject.GetType();

            foreach (var item in source)
            {
                someObjectType
                         .GetProperty(item.Key)
                         .SetValue(someObject, item.Value, null);
            }

            return someObject;
        }

        public static IDictionary<string, object> AsDictionary(this object source, BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
        {
            return source.GetType().GetProperties(bindingAttr).ToDictionary
            (
                propInfo => propInfo.Name,
                propInfo => propInfo.GetValue(source, null)
            );

        }

        /// <summary>
        /// Returns a list of the keys that were set, used for application arguments
        /// so we are only interested if the option was set, we don't need to log the content of the argument
        /// </summary>
        /// <param name="source"></param>
        /// <param name="bindingAttr"></param>
        /// <returns></returns>
        public static IDictionary<string, string> AsDictionaryForTelemetry(this object source, BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
        {
            return source.GetType().GetProperties(bindingAttr).Where(pi =>
            {
                switch (pi.PropertyType.Name)
                {
                    case "String": return !string.IsNullOrEmpty((string)pi.GetValue(source, null));
                    case "Boolean": return (bool)pi.GetValue(source, null);
                    case "Int32": return (int)pi.GetValue(source, null) != 0;
                };
                return false;
            }
            ).ToDictionary
            (
                propInfo => propInfo.Name,
                propInfo => "True" //propInfo.GetValue(source, null).ToString()
            ); 

        }
    }
}
