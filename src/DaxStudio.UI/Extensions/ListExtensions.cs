using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Extensions
{
    public static class ListExtensions
    {
        //you can use this extension method and call it like this.
        // DataTable dt = YourList.ToDataTable();

        public static DataTable ToDataTable<T>(this List<T> iList)

        {
            DataTable dataTable = new DataTable();
            PropertyDescriptorCollection propertyDescriptorCollection =
                TypeDescriptor.GetProperties(typeof(T));
            for (int i = 0; i < propertyDescriptorCollection.Count; i++)
            {
                PropertyDescriptor propertyDescriptor = propertyDescriptorCollection[i];
                Type type = propertyDescriptor.PropertyType;

                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    type = Nullable.GetUnderlyingType(type);


                dataTable.Columns.Add(propertyDescriptor.Name, type);
            }
            object[] values = new object[propertyDescriptorCollection.Count];
            foreach (T iListItem in iList)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = propertyDescriptorCollection[i].GetValue(iListItem);
                }
                dataTable.Rows.Add(values);
            }
            return dataTable;
        }

        public static LocaleIdentifier GetByLcid(this SortedList<string,LocaleIdentifier> locales, int localeId)
        {

            var loc = locales.FirstOrDefault(l => l.Value.LCID == localeId);
            if (loc.Value == null) {
                return locales["<Default>"];
            }
            return loc.Value;
        }

    }
        
}
