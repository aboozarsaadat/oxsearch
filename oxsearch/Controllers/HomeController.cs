using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using oxsearch.Data;
using oxsearch.Models;
using oxsearch.Services;

namespace oxsearch.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly SearchService _searchService;

        public HomeController(AppDbContext context, SearchService searchService)
        {
            _context = context;
            _searchService = searchService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("search")]
        public async Task<IActionResult> Results([FromQuery] string q, [FromQuery] int page = 1)
        {
            if (string.IsNullOrWhiteSpace(q))
                return RedirectToAction("Index");

            if (page < 1) page = 1;
            int pageSize = 10;

            var result = await _searchService.SearchAsync(q, page, pageSize);

            ViewBag.Query = q;
            return View(result);
        }

        [HttpGet("redirect")]
        public async Task<IActionResult> RedirectTo(int id)
        {
            var page = await _context.CrawledPages.FindAsync(id);
            if (page == null)
                return NotFound();

            await _searchService.IncrementVisitCountAsync(id);

            ViewBag.TargetUrl = page.Url;
            return View();
        }

        [HttpGet("submit")]
        public IActionResult SubmitSite()
        {
            return View();
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitSite(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                ViewBag.Message = "Ungültige URL.";
                return View();
            }

            var domain = $"{uri.Scheme}://{uri.Host}";

            var existing = await _context.Sites.FirstOrDefaultAsync(s => s.Domain == domain);
            if (existing == null)
            {
                _context.Sites.Add(new Site
                {
                    Domain = domain,
                    IsActive = true,
                    CrawlFrequencyHours = 24,
                    Priority = 3,
                    NextCrawled = DateTime.UtcNow
                });
            }
            else
            {
                existing.IsActive = true;
            }

            _context.UserRequests.Add(new UserRequest
            {
                Url = url,
                RequestType = "Add",
                Status = "Approved"
            });
            await _context.SaveChangesAsync();
            ViewBag.Message = "Website erfolgreich hinzugefügt. Sie wird bald gecrawlt.";

            return View();
        }
    }
}