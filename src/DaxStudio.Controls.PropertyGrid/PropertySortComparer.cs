using System;
using System.Collections;
using System.Collections.Generic;

namespace DaxStudio.Controls.PropertyGrid
{
    public class GenericComparer<T> : IComparer<T>, IComparer
        where T : IComparable<T>
    {


        public int Compare(T x, T y)
        {
            return x.CompareTo(y);
        }

        public int Compare(object x, object y)
        {
            return Compare((T)x, (T)y);
        }

    }
}
