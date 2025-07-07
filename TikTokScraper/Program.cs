using Microsoft.Playwright;
using System.Text;

class Program
{
    static async Task Main(string[] args)
    {
        await GetVideoLinks();
        // await GetVideoStuff();
    }
    static async Task GetVideoLinks()
    {
        var profileUrl = "https://www.tiktok.com/@22gradusi"; // 👈 change this
        var linksFile = "video_links.txt";
        int targetCount = 10;
        int scrollDelayMs = 1200;

        // Load previous links if file exists
        HashSet<string> allLinks = File.Exists(linksFile)
            ? new HashSet<string>(File.ReadAllLines(linksFile))
            : new HashSet<string>();

        HashSet<string> newLinks = new();

        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        try
        {
            await page.GotoAsync(profileUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            Console.WriteLine("✅ Page loaded. Starting scroll...");

            int scrolls = 0;
            while (newLinks.Count < targetCount)
            {
                // Extract current visible video links
                var links = await page.EvalOnSelectorAllAsync<string[]>(
                    "a[href*='/video/']",
                    "els => els.map(e => e.href)"
                );

                foreach (var link in links)
                {
                    if (!allLinks.Contains(link))
                    {
                        allLinks.Add(link);
                        newLinks.Add(link);
                        Console.WriteLine($"[{newLinks.Count}] {link}");

                        if (newLinks.Count >= targetCount)
                            break;
                    }
                }

                // Scroll to load more videos
                await page.EvaluateAsync("window.scrollBy(0, window.innerHeight)");
                await Task.Delay(scrollDelayMs);

                scrolls++;
                if (scrolls > 300)
                {
                    Console.WriteLine("⚠️ Stopping after 300 scrolls.");
                    break;
                }
            }

            Console.WriteLine($"✅ Finished. Total collected this run: {newLinks.Count}");

            // Save new links
            await File.AppendAllLinesAsync(linksFile, newLinks);

            Console.WriteLine($"🔽 Saved to {linksFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }

        Console.WriteLine("✅ Done. Press any key to exit...");
        Console.ReadKey();
    }

    static async Task GetVideoStuff()
    {
        var inputFile = "video_links.txt";
        var outputFile = "video_metadata.txt";

        var videoLinks = File.ReadAllLines(inputFile)
                             .Where(l => !string.IsNullOrWhiteSpace(l))
                             .Distinct()
                             .ToList();

        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        Console.WriteLine($"🔍 Scraping {videoLinks.Count} videos...\n");

        foreach (var url in videoLinks)
        {
            try
            {
                await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });
                await page.WaitForTimeoutAsync(1500); // Let JS render fully

                string likes = await GetText(page, "strong[data-e2e='like-count']");
                string comments = await GetText(page, "strong[data-e2e='comment-count']");
                string shares = await GetText(page, "strong[data-e2e='share-count']");
                string views = await GetText(page, "strong[data-e2e='view-count']"); // Usually not available

                string result = $"{url} : comments({comments}), likes({likes}), shares({shares}), views({views})";

                Console.WriteLine(result);
                await File.AppendAllTextAsync(outputFile, result + Environment.NewLine);
            }
            catch (Exception ex)
            {
                string failMsg = $"❌ Failed to scrape {url}: {ex.Message}";
                Console.WriteLine(failMsg);
                await File.AppendAllTextAsync(outputFile, failMsg + Environment.NewLine);
            }
        }

        Console.WriteLine($"\n✅ Done. Saved to '{outputFile}'.");
        Console.ReadKey();
    }

    static async Task<string> GetText(IPage page, string selector)
    {
        try
        {
            var el = await page.QuerySelectorAsync(selector);
            return el != null ? (await el.InnerTextAsync()) : "N/A";
        }
        catch
        {
            return "N/A";
        }
    }
}


/*using Microsoft.Playwright;

class Program
{
    public static async Task Main()
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false, // Try headless: true after debugging
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)...", // Spoof UA
        });

        var page = await context.NewPageAsync();

        var url = "https://www.tiktok.com/@22gradusi";
        await page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        // Wait for at least one video thumbnail to load
        await page.WaitForSelectorAsync("div[data-e2e='user-post-item']");

        var videoLinks = await page.EvalOnSelectorAllAsync<string[]>(
            "a[href*='/video/']",
            "els => els.map(e => e.href)"
        );

        foreach (var link in videoLinks.Distinct())
        {
            Console.WriteLine($"Video link: {link}");
        }

        await browser.CloseAsync();
    }
}
*/

/*using System.Threading.Tasks;
using Microsoft.Playwright;

class Program
{
    public static async Task Main()
    {
        var username = "22gradusi";
        var url = $"https://www.tiktok.com/@{username}";

        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                        "(KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36"
        });

        var page = await context.NewPageAsync();
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for profile info to load
        await page.WaitForSelectorAsync("h2[data-e2e='user-subtitle']");

        var name = await page.InnerTextAsync("h2[data-e2e='user-subtitle']");
        var followers = await page.InnerTextAsync("strong[data-e2e='followers-count']");
        var likes = await page.InnerTextAsync("strong[data-e2e='likes-count']");

        Console.WriteLine($"Username: {username}");
        Console.WriteLine($"Name: {name}");
        Console.WriteLine($"Followers: {followers}");
        Console.WriteLine($"Likes: {likes}");

        await browser.CloseAsync();
    }
}


*/