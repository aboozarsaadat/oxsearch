using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace oxsearch.Services
{
    public class HtmlParser
    {
        public (string title, string text, List<string> links) Parse(string html, Uri baseUri)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? "";
            title = Regex.Replace(title, @"\s+", " ");

            var body = doc.DocumentNode.SelectSingleNode("//body");
            string text = "";
            if (body != null)
            {
                foreach (var script in body.Descendants("script").ToArray())
                    script.Remove();
                foreach (var style in body.Descendants("style").ToArray())
                    style.Remove();
                text = body.InnerText;
            }
            text = Regex.Replace(text, @"\s+", " ").Trim();

            var links = new List<string>();
            var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");
            if (anchorNodes != null)
            {
                foreach (var node in anchorNodes)
                {
                    var href = node.GetAttributeValue("href", "");
                    if (string.IsNullOrWhiteSpace(href)) continue;
                    if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) continue;
                    if (href.StartsWith("#")) continue;

                    if (Uri.TryCreate(baseUri, href, out var absoluteUri))
                    {
                        var scheme = absoluteUri.Scheme.ToLower();
                        if (scheme == "http" || scheme == "https")
                            links.Add(absoluteUri.AbsoluteUri);
                    }
                }
            }

            return (title, text, links.Distinct().ToList());
        }
    }
}