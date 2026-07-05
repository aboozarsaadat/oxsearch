using Microsoft.EntityFrameworkCore;
using oxsearch.Data;
using oxsearch.Models;
using oxsearch.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=oxsearch.db"));

builder.Services.AddHttpClient();
builder.Services.AddScoped<HtmlParser>();
builder.Services.AddScoped<TextNormalizer>();
builder.Services.AddScoped<IndexingService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddHostedService<CrawlerService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();

    if (!context.Sites.Any())
    {
        context.Sites.AddRange(
            new Site { Domain = "https://en.wikipedia.org", IsActive = true, CrawlFrequencyHours = 24, Priority = 4, NextCrawled = DateTime.UtcNow },
            new Site { Domain = "https://www.spiegel.de", IsActive = true, CrawlFrequencyHours = 12, Priority = 5, NextCrawled = DateTime.UtcNow },
            new Site { Domain = "https://www.zeit.de", IsActive = true, CrawlFrequencyHours = 12, Priority = 5, NextCrawled = DateTime.UtcNow },
            new Site { Domain = "https://de.wikipedia.org", IsActive = true, CrawlFrequencyHours = 24, Priority = 4, NextCrawled = DateTime.UtcNow }
        );
        context.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();