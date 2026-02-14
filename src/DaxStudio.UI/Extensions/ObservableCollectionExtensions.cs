using System;
using System.Collections.Generic;

namespace DaxStudio.UI.Extensions
{
    public static class ObservableCollectionExtensions
    {
        public static void AddRange<T>(this System.Collections.ObjectModel.ObservableCollection<T> collection, IEnumerable<T> items)
            {
                if (collection == null) throw new ArgumentNullException(nameof(collection));
                if (items == null) throw new ArgumentNullException(nameof(items));
    
                foreach (var item in items)
                {
                    collection.Add(item);
                }
        }
    }
}
