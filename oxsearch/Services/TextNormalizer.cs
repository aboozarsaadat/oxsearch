using System.Text.RegularExpressions;

namespace oxsearch.Services
{
    public class TextNormalizer
    {
        private static readonly Dictionary<char, char> GermanUmlautMap = new()
        {
            { 'ä', 'a' }, { 'ö', 'o' }, { 'ü', 'u' }, { 'Ä', 'A' }, { 'Ö', 'O' }, { 'Ü', 'U' },
            { 'ß', 's' }
        };

        public string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = text.ToLowerInvariant();

            // Replace German umlauts and ß
            foreach (var kv in GermanUmlautMap)
                text = text.Replace(kv.Key, kv.Value);

            // Persian/Arabic normalization
            text = text.Replace('ي', 'ی').Replace('ك', 'ک');
            text = Regex.Replace(text, @"[إأآا]", "ا");
            text = Regex.Replace(text, @"[ؤ]", "و");
            text = Regex.Replace(text, @"[ئ]", "ی");

            // Remove zero-width joiners/non-joiners
            text = Regex.Replace(text, @"\u200C|\u200D", "");

            return text.Trim();
        }

        public List<string> Tokenize(string text)
        {
            var normalized = Normalize(text);
            // Split on any non-letter character (Unicode letters)
            var words = Regex.Split(normalized, @"[^\p{L}]+")
                .Where(w => w.Length > 1)
                .ToList();
            return words;
        }

        public string DetectLanguage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "unknown";

            int persianCount = Regex.Matches(text, @"[\u0600-\u06FF]").Count;
            int germanCount = Regex.Matches(text, @"[äöüßÄÖÜ]").Count;

            if (persianCount > text.Length * 0.2)
                return "fa";
            if (germanCount > 0)
                return "de";
            // Default to English if Latin characters dominate
            int latinCount = Regex.Matches(text, @"[a-zA-Z]").Count;
            if (latinCount > text.Length * 0.5)
                return "en";

            return "unknown";
        }
    }
}