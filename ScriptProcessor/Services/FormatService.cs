using System.Text.RegularExpressions;
namespace ScriptProcessor.Services
{
    public class FormatService : IFormatter
    {
        public string RemoveContractions(string text)
        {
            string pattern = @"\b(" + string.Join("|",
                 Map.Keys.Select(k => Regex.Escape(k).Replace("'", "['’]"))) + @")\b";

            return Regex.Replace(text, pattern, m =>
            {
                string key = m.Value.ToLowerInvariant().Replace('’', '\'');
                string repl = Map[key];

                // preserve original casing
                if (m.Value.All(char.IsUpper)) return repl.ToUpperInvariant();
                if (char.IsUpper(m.Value[0])) return char.ToUpper(repl[0]) + repl[1..];
                return repl.ToLowerInvariant();
            });
        }

        static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["i'll"] = "I will",
            ["we'll"] = "we will",
            ["you'll"] = "you will",
            ["he'll"] = "he will",
            ["she'll"] = "she will",
            ["they'll"] = "they will",
            ["it'll"] = "it will",

            ["i'm"] = "I am",
            ["we're"] = "we are",
            ["you're"] = "you are",
            ["they're"] = "they are",
            ["he's"] = "he is",
            ["she's"] = "she is",
            ["it's"] = "it is",

            ["i've"] = "I have",
            ["we've"] = "we have",
            ["you've"] = "you have",
            ["they've"] = "they have",

            ["don't"] = "do not",
            ["doesn't"] = "does not",
            ["didn't"] = "did not",
            ["can't"] = "cannot",
            ["won't"] = "will not",
            ["isn't"] = "is not",
            ["aren't"] = "are not",
            ["wasn't"] = "was not",
            ["weren't"] = "were not",
            ["shouldn't"] = "should not",
            ["wouldn't"] = "would not",
            ["couldn't"] = "could not",

            ["let's"] = "let us",
            ["what's"] = "what is",
            ["that's"] = "that is",
            ["there's"] = "there is",
            ["who'll"] = "who will",
            ["there'll"] = "there will"
        };
    }
}
