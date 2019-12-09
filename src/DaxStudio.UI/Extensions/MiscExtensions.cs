using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Extensions
{
    /// <summary>
    /// Misc extensions.
    /// </summary>
    public static class MiscExtensions
    {
        /// <summary>
        /// Determines whether an object is equal to any of the elements in a sequence.
        /// </summary>
        [Pure]
        public static bool IsEither<T>(this T obj,  IEnumerable<T> variants,
             IEqualityComparer<T> comparer)
        {
            // todo check for null arguments

            //variants.GuardNotNull(nameof(variants));
            //comparer.GuardNotNull(nameof(comparer));

            return variants.Contains(obj, comparer);
        }

        /// <summary>
        /// Determines whether an object is equal to any of the elements in a sequence.
        /// </summary>
        [Pure]
        public static bool IsEither<T>(this T obj, IEnumerable<T> variants)
            => IsEither(obj, variants, EqualityComparer<T>.Default);

        /// <summary>
        /// Determines whether the object is equal to any of the parameters.
        /// </summary>
        [Pure]
        public static bool IsEither<T>(this T obj, params T[] variants) => IsEither(obj, (IEnumerable<T>)variants);
    }
}
