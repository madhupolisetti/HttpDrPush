using System;

namespace ExtensionMethods
{
    public static class MyExtensions
    {
        public static int WordCount(this String str)
        {
            return str.Split(new char[] { ' ', '.', '?' },
                             StringSplitOptions.RemoveEmptyEntries).Length;
        }
        public static bool IsDBNull(this object input)
        {
            return input.Equals(System.DBNull.Value);
        }
        public static string ReplaceWhiteSpaces(this string input)
        {
            return input.Replace(" ", "").Replace(Environment.NewLine, "");
        }
    }
}