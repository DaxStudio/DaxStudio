using System;

namespace DaxStudio.CommandLine.Extensions
{
    internal static class StringExtensions
    {
        public static string ToDaxName(this string name)
        {
            return $"'{name}'";
        }

        public static bool ContainsEither(this string[] arr, string value1, string value2)
        {
            for (var idx = arr.Length - 1; idx >= 0; idx--)
            {
                var item = arr[idx];
                if (string.Compare(item, value1, true) == 0 || string.Compare(item, value2, true) == 0)
                {
                    return true;
                }
            }
            return false;
        }


        // Removes a[i..i+n], preserving the order of array elements.
        public static void RemoveAt(ref string[] a, int i, int n)
        {
            // Create a Span that references the array elements.
            var s = a.AsSpan();
            // Move array elements that follow the ones to remove to the front.
            // Caveat: Use `s`, not `a`, or else the result may be invalid.
            s.Slice(i + n).CopyTo(s.Slice(i, s.Length - i - n));
            // Cut the last n array elements off.
            a = s.Slice(0, s.Length - n).ToArray();
        }
    }
}



