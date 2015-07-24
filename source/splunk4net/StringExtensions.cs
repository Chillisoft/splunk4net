using System.Text.RegularExpressions;

namespace splunk4net
{
    // kudos to mindplay.dk, http://stackoverflow.com/questions/188892/glob-pattern-matching-in-net
    // provides a shim for simple glob matching to Regex
    public static class StringExtensions
    {
        public static bool Like(this string str, string pattern)
        {
            return new Regex(
                "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            ).IsMatch(str);
        }
    }
}
