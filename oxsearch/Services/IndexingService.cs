using Microsoft.EntityFrameworkCore;
using oxsearch.Data;
using oxsearch.Models;

namespace oxsearch.Services
{
    public class IndexingService
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

        public IndexingService(AppDbContext context, TextNormalizer normalizer)
        {
            _context = context;
            _normalizer = normalizer;
        }

        public async Task IndexPageAsync(int pageId, string content)
        {
            var oldEntries = await _context.IndexEntries
                .Where(e => e.PageId == pageId)
                .ToListAsync();
            _context.IndexEntries.RemoveRange(oldEntries);

            var words = _normalizer.Tokenize(content)
                .Where(w => !StopWords.Contains(w))
                .ToList();

            var frequencyMap = words.GroupBy(w => w)
                .ToDictionary(g => g.Key, g => g.Count());

            var newEntries = frequencyMap.Select(kvp => new IndexEntry
            {
                Word = kvp.Key,
                PageId = pageId,
                Count = kvp.Value
            });

            await _context.IndexEntries.AddRangeAsync(newEntries);
            await _context.SaveChangesAsync();
        }
    }
}