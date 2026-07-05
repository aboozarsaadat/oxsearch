using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace oxsearch.Models
{
    public class IndexEntry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Word { get; set; } = string.Empty;

        public int PageId { get; set; }

        [ForeignKey("PageId")]
        public CrawledPage Page { get; set; } = null!;

        public int Count { get; set; }
    }
}