using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using AlphaLaunch.Core.Debug;
using AlphaLaunch.Core.Indexes;

namespace AlphaLaunch.Core.Selecta
{
    public class Searcher
    {
        private readonly IIndexable[] _allItems;
        private readonly IIndexable[] _matchedItems;
        private readonly SelectaSearcher _selecta;

        public string SearchString { get; }
        public SearchResult[] SearchResults { get; }

        public static Searcher Create(IIndexable[] allItems)
        {
            return new Searcher(allItems, allItems, string.Empty, new SearchResult[0]);
        }

        private Searcher(IIndexable[] allItems, IIndexable[] matchedItems, string searchString, SearchResult[] searchResults)
        {
            _allItems = allItems;
            _matchedItems = matchedItems;
            SearchString = searchString;
            SearchResults = searchResults;
            _selecta = new SelectaSearcher();
        }

        public Searcher Search(string searchString)
        {
            searchString = searchString ?? string.Empty;

            var items = _matchedItems;

            if (!searchString.StartsWith(SearchString))
            {
                items = _allItems;
            }

            var scoreStopwatch = Stopwatch.StartNew();

            var matches = items
                .Select(x => new { MatchScore = _selecta.Score(searchString, x.Name), Indexable = x })
                .Where(x => x.MatchScore.Score != int.MaxValue)
                .ToArray();

            var searchResults = matches
                .OrderBy(x => x.MatchScore.Score)
                .ThenBy(x => x.Indexable.Name.Length)
                .ThenBy(x => x.MatchScore.Range.EndIndex - x.MatchScore.Range.StartIndex)
                .Select(x => new SearchResult(x.Indexable.Name, x.MatchScore.Score, x.Indexable, ImmutableDictionary.Create(Enumerable.Range(x.MatchScore.Range.StartIndex, 1 + x.MatchScore.Range.EndIndex - x.MatchScore.Range.StartIndex).Select(i => new KeyValuePair<int, double>(i, 0.0)))))
                .Take(50)
                .ToArray();

            var matchedItems = matches.Select(x => x.Indexable).ToArray();

            scoreStopwatch.Stop();

            Log.Info($"Found {matches.Length} results of {items.Length} [ scr: {scoreStopwatch.ElapsedMilliseconds} ms ]");

            return new Searcher(_allItems, matchedItems, searchString, searchResults);
        }
    }
}