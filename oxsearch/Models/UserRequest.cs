using System.ComponentModel.DataAnnotations;

namespace oxsearch.Models
{
    public class UserRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Url { get; set; } = string.Empty;

        [Required]
        public string RequestType { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending";

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}