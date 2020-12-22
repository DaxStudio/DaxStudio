using System;

namespace ADOTabular.Utils
{
    public static class TupleExtensions
    {
        public static void Deconstruct<T1, T2>(this Tuple<T1, T2> tuple, out T1 item1, out T2 item2)
        {
            if (tuple == null) { 
                item1 = default; 
                item2 = default; 
                return; 
            }
            item1 = tuple.Item1;
            item2 = tuple.Item2;
        }
    }
}
