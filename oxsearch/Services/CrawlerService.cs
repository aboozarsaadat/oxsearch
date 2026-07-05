using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using oxsearch.Data;
using oxsearch.Models;
using System.Net.Http;

namespace oxsearch.Services
{
    public class CrawlerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CrawlerService> _logger;
        private static readonly TimeSpan CrawlInterval = TimeSpan.FromMinutes(5);

        public CrawlerService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory,
            ILogger<CrawlerService> logger)
        {
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DoCrawlAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler im Crawl-Zyklus");
                }

                await Task.Delay(CrawlInterval, stoppingToken);
            }
        }

        private async Task DoCrawlAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var parser = scope.ServiceProvider.GetRequiredService<HtmlParser>();
            var indexing = scope.ServiceProvider.GetRequiredService<IndexingService>();
            var normalizer = scope.ServiceProvider.GetRequiredService<TextNormalizer>();
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(15);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("oxsearch-crawler/1.0");

            var now = DateTime.UtcNow;
            var sites = await context.Sites
                .Where(s => s.IsActive && (s.NextCrawled == null || s.NextCrawled <= now))
                .OrderByDescending(s => s.Priority)
                .ThenBy(s => s.NextCrawled)
                .ToListAsync(stoppingToken);

            foreach (var site in sites)
            {
                stoppingToken.ThrowIfCancellationRequested();
                try
                {
                    int maxPages = site.Priority >= 5 ? 30 : 15;
                    await CrawlSiteAsync(site, context, parser, indexing, normalizer, httpClient, maxPages, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler beim Crawlen von {Domain}", site.Domain);
                }
            }
        }

        private async Task CrawlSiteAsync(Site site, AppDbContext context, HtmlParser parser,
            IndexingService indexing, TextNormalizer normalizer, HttpClient httpClient, int maxPages, CancellationToken token)
        {
            var domainUri = new Uri(site.Domain);
            var baseHost = domainUri.Host.ToLowerInvariant();
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(site.Domain);
            visited.Add(site.Domain);

            int pagesCrawled = 0;

            while (queue.Count > 0 && pagesCrawled < maxPages && !token.IsCancellationRequested)
            {
                var url = queue.Dequeue();
                if (!url.StartsWith("http")) continue;

                HttpResponseMessage response;
                try
                {
                    response = await httpClient.GetAsync(url, token);
                    if (!response.IsSuccessStatusCode) continue;
                }
                catch
                {
                    continue;
                }

                var html = await response.Content.ReadAsStringAsync();
                var (title, text, links) = parser.Parse(html, new Uri(url));

                // Detect language from combined title + text
                string language = normalizer.DetectLanguage(title + " " + (text.Length > 500 ? text.Substring(0, 500) : text));

                var existingPage = await context.CrawledPages
                    .FirstOrDefaultAsync(p => p.Url == url, token);
                CrawledPage page;
                if (existingPage != null)
                {
                    page = existingPage;
                    page.Title = title;
                    page.Content = text;
                    page.LastCrawled = DateTime.UtcNow;
                    page.Language = language;
                }
                else
                {
                    page = new CrawledPage
                    {
                        Url = url,
                        Title = title,
                        Content = text,
                        LastCrawled = DateTime.UtcNow,
                        SiteId = site.Id,
                        Language = language
                    };
                    context.CrawledPages.Add(page);
                    await context.SaveChangesAsync(token);
                }

                await indexing.IndexPageAsync(page.Id, text);

                foreach (var link in links)
                {
                    if (visited.Count >= maxPages * 2) break;
                    if (visited.Contains(link)) continue;
                    try
                    {
                        var linkUri = new Uri(link);
                        if (linkUri.Host.ToLowerInvariant() == baseHost)
                        {
                            visited.Add(link);
                            queue.Enqueue(link);
                        }
                    }
                    catch { }
                }

                pagesCrawled++;
            }

            site.LastCrawled = DateTime.UtcNow;
            site.NextCrawled = DateTime.UtcNow.AddHours(site.CrawlFrequencyHours);
            await context.SaveChangesAsync(token);
        }
    }
}