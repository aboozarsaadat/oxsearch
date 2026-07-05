using Microsoft.EntityFrameworkCore;
using oxsearch.Data;

namespace oxsearch.Services
{
    public class SearchService
    {
        private readonly AppDbContext _context;
        private readonly TextNormalizer _normalizer;

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            // English
            "the","a","an","in","on","at","to","for","of","and","is","are","was","were",
            "be","been","being","have","has","had","do","does","did","but","or","not",
            "so","if","then","than","that","this","these","those","it","its","with",
            "from","as","by","i","you","he","she","we","they","my","your","his","her",
            "our","their","me","him","us","them","all","any","each","every","which",
            "who","whom","what","when","where","how","no","also","can","may","will",
            "would","could","should","into","over","up","down","out","just","very","too",
            // German
            "der","die","das","den","dem","des","ein","eine","einer","eines","einem",
            "einen","und","oder","aber","nicht","als","dass","dann","weil","ob","wenn",
            "wie","wo","was","wer","wen","wem","wessen","welcher","welche","welches",
            "dieser","diese","dieses","jener","jene","jenes","er","sie","es","ihm","ihn",
            "ihr","wir","uns","euch","sie","sich","mich","dich","mir","dir","sein","haben",
            "werden","können","müssen","sollen","wollen","dürfen","mögen","tun","machen",
            "sagen","gehen","kommen","geben","nehmen","sehen","finden","wissen","lassen",
            "stehen","liegen","halten","heißen","zeigen","führen","bringen","sprechen",
            "werden","war","ist","sind","seid","gewesen","worden","hat","hatte","haben",
            "hatten","kann","konnte","können","konnten","muss","musste","müssen","mussten",
            "soll","sollte","sollen","sollten","will","wollte","wollen","wollten","darf",
            "durfte","dürfen","durften","mag","mochte","mögen","mochten",
            // Persian
            "از","با","به","برای","در","که","و","این","آن","ها","های","را","تا",
            "است","بود","شد","کرد","گفت","دارد","باشد","هست","می","هم","نه",
            "یا","اگر","اما","چون","چه","چگونه","کجا","چقدر","چرا","آیا","حتا",
            "بله","خیر","بلکه","زیرا","بنابراين","پس","لذا","مثل","مانند","نيز",
            "البته","يعنی","هر","همه","هيچ","بعضی","چند","زياد","کم","ديگر",
            "اول","دوم","سوم","قبل","بعد","همان","همين","همچنين","همانند","همانطور"
        };

        public SearchService(AppDbContext context, TextNormalizer normalizer)
        {
            _context = context;
            _normalizer = normalizer;
        }

        public async Task<PaginatedSearchResult> SearchAsync(string query, int page = 1, int pageSize = 10)
        {
            var rawTokens = _normalizer.Tokenize(query)
                .Where(w => !StopWords.Contains(w))
                .Distinct()
                .ToList();

            if (!rawTokens.Any())
                return new PaginatedSearchResult { Items = new List<SearchResult>(), TotalResults = 0, Page = page, PageSize = pageSize };

            var tokens = rawTokens.Select(t => _normalizer.Normalize(t)).ToList();

            var totalPagesCount = await _context.CrawledPages.CountAsync();
            if (totalPagesCount == 0)
                return new PaginatedSearchResult { Items = new List<SearchResult>(), TotalResults = 0, Page = page, PageSize = pageSize };

            var pageIds = await _context.IndexEntries
                .Where(e => tokens.Contains(e.Word))
                .GroupBy(e => e.PageId)
                .Where(g => g.Select(x => x.Word).Distinct().Count() == tokens.Count)
                .Select(g => g.Key)
                .ToListAsync();

            if (!pageIds.Any())
                return new PaginatedSearchResult { Items = new List<SearchResult>(), TotalResults = 0, Page = page, PageSize = pageSize };

            var docFreq = new Dictionary<string, int>();
            foreach (var token in tokens)
            {
                var freq = await _context.IndexEntries
                    .Where(e => e.Word == token)
                    .Select(e => e.PageId)
                    .Distinct()
                    .CountAsync();
                docFreq[token] = freq;
            }

            var entries = await _context.IndexEntries
                .Where(e => pageIds.Contains(e.PageId) && tokens.Contains(e.Word))
                .ToListAsync();

            var pageScores = new Dictionary<int, double>();
            foreach (var group in entries.GroupBy(e => e.PageId))
            {
                double score = 0;
                foreach (var entry in group)
                {
                    if (docFreq.TryGetValue(entry.Word, out int df) && df > 0)
                    {
                        double tf = entry.Count;
                        double idf = Math.Log((double)totalPagesCount / df);
                        score += tf * idf;
                    }
                }
                pageScores[group.Key] = score;
            }

            var sortedPageIds = pageScores.OrderByDescending(kvp => kvp.Value)
                .Take(100)
                .Select(kvp => kvp.Key)
                .ToList();

            var pages = await _context.CrawledPages
                .Where(p => sortedPageIds.Contains(p.Id))
                .ToListAsync();

            var recentThreshold = DateTime.UtcNow.AddDays(-2);

            var finalOrder = pages
                .GroupBy(p => p.LastCrawled >= recentThreshold)
                .OrderByDescending(g => g.Key)
                .SelectMany(g => g.Key
                    ? g.OrderByDescending(p => p.LastCrawled)
                    : g.OrderByDescending(p => p.VisitCount))
                .ToList();

            var totalResults = finalOrder.Count;
            var paged = finalOrder
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new SearchResult
                {
                    PageId = p.Id,
                    Title = string.IsNullOrWhiteSpace(p.Title) ? p.Url : p.Title,
                    Url = p.Url,
                    Snippet = GenerateSnippet(p.Content, rawTokens)
                })
                .ToList();

            return new PaginatedSearchResult
            {
                Items = paged,
                TotalResults = totalResults,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task IncrementVisitCountAsync(int pageId)
        {
            var page = await _context.CrawledPages.FindAsync(pageId);
            if (page != null)
            {
                page.VisitCount++;
                await _context.SaveChangesAsync();
            }
        }

        private string GenerateSnippet(string content, List<string> tokens)
        {
            if (string.IsNullOrEmpty(content)) return "";
            var lower = _normalizer.Normalize(content);
            int bestPos = -1;
            foreach (var token in tokens)
            {
                var normalizedToken = _normalizer.Normalize(token);
                int pos = lower.IndexOf(normalizedToken);
                if (pos >= 0)
                {
                    bestPos = pos;
                    break;
                }
            }
            if (bestPos < 0) return content.Length > 200 ? content.Substring(0, 200) : content;

            int start = Math.Max(0, bestPos - 80);
            int end = Math.Min(content.Length, bestPos + 120);
            string snippet = content.Substring(start, end - start);
            if (start > 0) snippet = "..." + snippet;
            if (end < content.Length) snippet = snippet + "...";
            return snippet;
        }
    }

    public class SearchResult
    {
        public int PageId { get; set; }
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Snippet { get; set; } = "";
    }

    public class PaginatedSearchResult
    {
        public List<SearchResult> Items { get; set; } = new();
        public int TotalResults { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalResults / PageSize);
    }
}