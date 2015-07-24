using System.Collections.Generic;
using Splunk.Client;

namespace splunk4net
{
    public static class SearchResultExtensions
    {
        public static IEnumerable<SearchResult> SearchResults(this SearchResultStream stream)
        {
            using (var enumerator = stream.GetEnumerator())
            {
                while (enumerator.MoveNext())
                    yield return enumerator.Current;
            }
        }
    }
}
