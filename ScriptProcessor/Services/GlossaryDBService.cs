using Microsoft.EntityFrameworkCore;
using ScriptProcessor.Data;
using ScriptProcessor.Models;
using System.Text.RegularExpressions;

namespace ScriptProcessor.Services
{
    public class GlossaryDBService
    {
        private readonly GlossaryDbContext _dbContext;

        public GlossaryDBService(GlossaryDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Dictionary<string, string>> GetTermsAsync(string targLanguage)//this gets the entire glossary
        {
            try
            {
                var dict = new Dictionary<string, string>();

                using var command = _dbContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = @"
                    SELECT EnglishTerm, TargetTerm
                    FROM TermTranslations
                    WHERE TargetLanguage = @targetLanguage";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@targetLanguage";
                parameter.Value = targLanguage;
                command.Parameters.Add(parameter);

                await _dbContext.Database.OpenConnectionAsync();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var englishTerm = reader.GetString(0); // EnglishTerm is first column
                    var targetTerm = reader.GetString(1);  // TargetTerm is second column
                    dict[englishTerm] = targetTerm;
                }

                return dict;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (_dbContext.Database.GetDbConnection().State == System.Data.ConnectionState.Open)
                {
                    await _dbContext.Database.CloseConnectionAsync();
                }
            }
        }



        #region Glossary searcher

        static Regex BuildGlossaryRegex(IEnumerable<string> terms)
        {
            // escape each term, allow flexible spaces inside phrases
            var alts = terms
                .Select(t => Regex.Escape(t.Trim()))
                .Select(t => t.Replace(@"\ ", @"\s+"))
                .OrderByDescending(t => t.Length);           // prefer longer (phrases) first

            // custom “word” boundary so letters/digits/_ count as word chars
            string left = @"(?<![\p{L}\p{N}_])";
            string right = @"(?![\p{L}\p{N}_])";

            // atomic group (?>...) avoids backtracking that could pick a shorter alt
            var pattern = $"{left}(?>(?:{string.Join("|", alts)})){right}";
            return new Regex(pattern, RegexOptions.IgnoreCase |
                                       RegexOptions.CultureInvariant |
                                       RegexOptions.Compiled);
        }

        static List<(string term, int index)> FindTerms(string script,
                                                        Regex rx,
                                                        Dictionary<string, string> glossary)
        {
            var results = new List<(string, int)>();
            foreach (Match m in rx.Matches(script))
            {
                // normalize the matched text back to the glossary key form
                // (case-insensitive; pick the first key that matches)
                var key = glossary.Keys.FirstOrDefault(k =>
                    string.Equals(k, m.Value, StringComparison.OrdinalIgnoreCase));

                if (key != null)
                {
                    results.Add((key, m.Index));
                }
            }
            return results;
        }


        public async Task<Dictionary<string, string>> SelectedWords(string scriptText, string targetLanguage = "french")
        {
            // Get the entire glossary dictionary
            var glossary = await GetTermsAsync(targetLanguage);

            // Return empty dictionary if no glossary terms or empty script
            if (glossary.Count == 0 || string.IsNullOrWhiteSpace(scriptText))
                return new Dictionary<string, string>();

            var rx = BuildGlossaryRegex(glossary.Keys);
            var hits = FindTerms(scriptText, rx, glossary);

            // Create dictionary of found terms and their translations
            var matchedPairs = new Dictionary<string, string>();

            // Group hits by term to avoid duplicates
            var foundTerms = hits.GroupBy(h => h.term)
                                .Select(g => g.Key)
                                .Distinct();

            // Add found terms and their translations to the result dictionary
            foreach (var term in foundTerms)
            {
                if (glossary.ContainsKey(term))
                {
                    matchedPairs[term] = glossary[term];
                }
            }

            return matchedPairs;
        }

        public async Task<Dictionary<string, (string translation, int count)>> SelectedWordsWithCounts(string scriptText, string targetLanguage = "french")
        {
            // Get the entire glossary dictionary
            var glossary = await GetTermsAsync(targetLanguage);

            // Return empty dictionary if no glossary terms or empty script
            if (glossary.Count == 0 || string.IsNullOrWhiteSpace(scriptText))
                return new Dictionary<string, (string translation, int count)>();

            var rx = BuildGlossaryRegex(glossary.Keys);
            var hits = FindTerms(scriptText, rx, glossary);

            // Create dictionary of found terms with their translations and counts
            var matchedPairsWithCounts = new Dictionary<string, (string translation, int count)>();

            // Group hits by term to get counts
            var counts = hits.GroupBy(h => h.term)
                            .ToDictionary(g => g.Key, g => g.Count());

            // Add found terms, their translations, and counts to the result dictionary
            foreach (var termCount in counts)
            {
                var term = termCount.Key;
                var count = termCount.Value;

                if (glossary.ContainsKey(term))
                {
                    matchedPairsWithCounts[term] = (glossary[term], count);
                }
            }

            return matchedPairsWithCounts;
        }
        #endregion
    }

}
