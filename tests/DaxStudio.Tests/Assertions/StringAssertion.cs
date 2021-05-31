using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Tests.Assertions
{
    public static class StringAssertion
    {
        //public static void ShouldEqualWithDiff(this string actualValue, string expectedValue)
        //{
        //    ShouldEqualWithDiff(actualValue, expectedValue, DiffStyle.Full, Console.Out);
        //}

        //public static void ShouldEqualWithDiff(this string actualValue, string expectedValue, DiffStyle diffStyle)
        //{
        //    ShouldEqualWithDiff(actualValue, expectedValue, diffStyle, Console.Out);
        //}
        [DebuggerStepThrough]
        public static void ShouldEqualWithDiff(string expectedValue, string actualValue, DiffStyle diffStyle)
        {
            if (actualValue == null || expectedValue == null)
            {
                //Assert.AreEqual(expectedValue, actualValue);
                Assert.AreEqual(expectedValue, actualValue);
                return;
            }

            if (actualValue.Equals(expectedValue, StringComparison.Ordinal)) return;

            StringBuilder output = new StringBuilder();
            if (diffStyle == DiffStyle.Compact) CompactDiff(actualValue, expectedValue, output);
            else DetailDiff( expectedValue, actualValue, diffStyle, output);

            throw new Exception(string.Format("\nExpected: {0}\nActual  : {1}\nDetails : {2}", expectedValue, actualValue, output.ToString()));
        }

        private static void CompactDiff(string expectedValue, string actualValue, StringBuilder output)
        {
            int maxLen = Math.Max(actualValue.Length, expectedValue.Length);
            int minLen = Math.Min(actualValue.Length, expectedValue.Length);
            for (int i = 0; i < maxLen; i++)
            {
                output.Append(i < minLen && actualValue[i] == expectedValue[i] ? " " : "^"); // put a mark under a differing character
            }
        }

        private static void DetailDiff(string expectedValue, string actualValue, DiffStyle diffStyle, StringBuilder output)
        {
            output.AppendLine();
            output.AppendLine("  Idx Expected  Actual");
            output.AppendLine("-------------------------");
            int maxLen = Math.Max(actualValue.Length, expectedValue.Length);
            int minLen = Math.Min(actualValue.Length, expectedValue.Length);
            for (int i = 0; i < maxLen; i++)
            {
                if (diffStyle != DiffStyle.Minimal || i >= minLen || actualValue[i] != expectedValue[i])
                {
                    output.AppendFormat("{0} {1,-3} {2,-4} {3,-3}  {4,-4} {5,-3}\n",
                        i < minLen && actualValue[i] == expectedValue[i] ? " " : "*", // put a mark beside a differing row
                        i, // the index
                        i < expectedValue.Length ? ((int)expectedValue[i]).ToString() : "", // character decimal value
                        i < expectedValue.Length ? expectedValue[i].ToSafeString() : "", // character safe string
                        i < actualValue.Length ? ((int)actualValue[i]).ToString() : "", // character decimal value
                        i < actualValue.Length ? actualValue[i].ToSafeString() : "" // character safe string
                    );
                }
            }
        }
        [DebuggerStepThrough]
        public static void ShouldEqualWithDiff(string expectedValue, string actualValue)
        {
            ShouldEqualWithDiff(expectedValue, actualValue, DiffStyle.Compact);
        }

        private static string ToSafeString(this char c)
        {
            if (Char.IsControl(c) || Char.IsWhiteSpace(c))
            {
                switch (c)
                {
                    case '\r':
                        return @"\r";
                    case '\n':
                        return @"\n";
                    case '\t':
                        return @"\t";
                    case '\a':
                        return @"\a";
                    case '\v':
                        return @"\v";
                    case '\f':
                        return @"\f";
                    default:
                        return String.Format("\\u{0:X};", (int)c);
                }
            }
            return c.ToString(CultureInfo.InvariantCulture);
        }
    }

    public enum DiffStyle
    {
        Full,
        Minimal,
        Compact
    }
}
