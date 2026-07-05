using Microsoft.EntityFrameworkCore;
using oxsearch.Models;

namespace oxsearch.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<CrawledPage> CrawledPages { get; set; }
        public DbSet<IndexEntry> IndexEntries { get; set; }
        public DbSet<Site> Sites { get; set; }
        public DbSet<UserRequest> UserRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IndexEntry>()
                .HasIndex(e => e.Word);

            modelBuilder.Entity<IndexEntry>()
                .HasIndex(e => new { e.Word, e.PageId });

            modelBuilder.Entity<CrawledPage>()
                .HasIndex(p => p.Url)
                .IsUnique();

            modelBuilder.Entity<Site>()
                .HasIndex(s => s.Domain)
                .IsUnique();
        }
    }
}