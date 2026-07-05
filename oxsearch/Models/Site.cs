using System.ComponentModel.DataAnnotations;

namespace oxsearch.Models
{
    public class Site
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Domain { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public int CrawlFrequencyHours { get; set; } = 24;

        public int Priority { get; set; } = 1;

        public DateTime? LastCrawled { get; set; }

        public DateTime? NextCrawled { get; set; }

        public ICollection<CrawledPage> CrawledPages { get; set; } = new List<CrawledPage>();
    }
}