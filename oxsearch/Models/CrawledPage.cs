using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace oxsearch.Models
{
    public class CrawledPage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Url { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public DateTime LastCrawled { get; set; } = DateTime.UtcNow;

        public int SiteId { get; set; }

        [ForeignKey("SiteId")]
        public Site Site { get; set; } = null!;

        public int VisitCount { get; set; } = 0;

        [MaxLength(10)]
        public string Language { get; set; } = "unknown";

        public ICollection<IndexEntry> IndexEntries { get; set; } = new List<IndexEntry>();
    }
}