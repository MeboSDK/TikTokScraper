using Microsoft.Playwright;
using System.Text;
using TikTokScraper;

class Program
{
    static async Task Main(string[] args)
    {
        var username = TakeUsername();
        var targetCount = TakeTargetCount();

        var result = await GetVideoLinksAndViews(username, targetCount);
        var metadata = await GetVideoStuff(result);

        CreateCSVFile(metadata, username);
    }

    static async Task<List<(string Url, string Views)>> GetVideoLinksAndViews(string username, int targetCount)
    {
        var profileUrl = "https://www.tiktok.com/@" + username;

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(profileUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
        Console.WriteLine("✅ Page loaded. Starting scroll…");

        var results = new List<(string Url, string Views)>();
        int scrolls = 0;
        int maxCount = 140;
        int scrollDelayMs = 1200;
        bool browserClosedByUser = false;

        browser.Disconnected += (_, _) =>
        {
            Console.WriteLine("⚠️ Browser was closed by the user!");
            browserClosedByUser = true;
        };

        while (results.Count < targetCount && scrolls < maxCount)
        {
            if (browserClosedByUser)
                break;

            try
            {
                // Ensure enough items have loaded
                await page.WaitForSelectorAsync("div[data-e2e='user-post-item']");

                // Grab all containers
                var items = await page.QuerySelectorAllAsync("div[data-e2e='user-post-item']");

                foreach (var item in items)
                {
                    // Extract the <a> href
                    var linkHandle = await item.QuerySelectorAsync("a[href*='/video/']");
                    if (linkHandle == null) continue;
                    var url = await linkHandle.GetAttributeAsync("href");

                    // Extract the <strong> text (views)
                    var viewsHandle = await item.QuerySelectorAsync("strong[data-e2e='video-views']");
                    var views = viewsHandle is not null
                        ? (await viewsHandle.InnerTextAsync()).Trim()
                        : "N/A";

                    // Avoid duplicates
                    if (results.Any(r => r.Url == url)) continue;
                    results.Add((url, views));

                    Console.WriteLine($"[{results.Count}] {url}  —  {views} - Scroll Count {scrolls}");

                    if (results.Count >= targetCount)
                        break;
                }

                if (results.Count >= targetCount)
                    break;
                // Scroll to load more videos
                await page.EvaluateAsync("window.scrollBy(0, window.innerHeight)");
                await Task.Delay(scrollDelayMs);
                Console.WriteLine("Scroll Count : " + scrolls + "/" + maxCount);
                scrolls++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during scroll {scrolls}: {ex.Message}");
                break; // Exit on error
            }
        }

        Console.WriteLine($"✅ Finished. Collected {results.Count} items.");

        return results;
    }

    static async Task<List<VideoMetadata>> GetVideoStuff(List<(string Url, string Views)> urlViews)
    {
        List<VideoMetadata> videoMetadatas = new();

        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        Console.WriteLine($"🔍 Scraping {urlViews.Count} videos...\n");
        int count = 0;
        foreach (var urlView in urlViews)
        {
            try
            {
                count++;
                await page.GotoAsync(urlView.Url, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });
                await page.WaitForTimeoutAsync(1500); // Let JS render fully

                string likes = await GetText(page, "strong[data-e2e='like-count']");
                string comments = await GetText(page, "strong[data-e2e='comment-count']");
                string shares = await GetText(page, "strong[data-e2e='share-count']");

                string uploadTime = await GetText(page, "span[data-e2e='browser-nickname'] span:last-child");

                VideoMetadata videoMetadata = new VideoMetadata
                {
                    Url = urlView.Url,
                    Views = urlView.Views,
                    Likes = likes,
                    Comments = comments,
                    Shares = shares,
                    UploadTime = uploadTime
                };

                string result = $"({count}) - {urlView.Url} : views({urlView.Views}), likes({likes}), comments({comments}), shares({shares}), " +
                $"uploaded({uploadTime})";

                Console.WriteLine(result);

                videoMetadatas.Add(videoMetadata);
            }
            catch (Exception ex)
            {
                string failMsg = $"At count {count} ❌ Failed to scrape {urlView.Url}: {ex.Message}";
                Console.WriteLine(failMsg);
            }
        }

        Console.WriteLine($"\n✅ Done. Saved to List.");
        return videoMetadatas;
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

    static void CreateCSVFile(List<VideoMetadata> metadatas, string userName)
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string csvPath = Path.Combine(desktopPath, $"{userName}.csv");

        using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
        {
            // Write header
            writer.WriteLine("Url,Views,Likes,Comments,Shares,UploadTime");

            // Write each row
            foreach (var metaData in metadatas)
            {
                string line = $"\"{metaData.Url}\"," +
                              $"\"{metaData.Views}\"," +
                              $"\"{metaData.Likes}\"," +
                              $"\"{metaData.Comments}\"," +
                              $"\"{metaData.Shares}\"," +
                              $"\"{metaData.UploadTime}\",";

                writer.WriteLine(line);
            }
        }

        Console.WriteLine($"✅ CSV saved to: {Path.GetFullPath(csvPath)}");
    }

    static string TakeUsername()
    {
        Console.Write("Write Username : ");

        var username = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(username))
        {
            Console.WriteLine("❌ Username cannot be empty.");
            throw new Exception("No no no");
        }

        return username.Trim().Replace(" ", "");
    }

    static int TakeTargetCount()
    {
        Console.Write("Write Video Count : ");

        var input = Console.ReadLine();
        var tryparse = int.TryParse(input, out int count);

        if (input.Any(char.IsWhiteSpace))
        {
            Console.WriteLine("Error: no spaces allowed.");
        }
        else if (tryparse)
        {
            Console.WriteLine($"You entered the integer {count}.");
        }
        else
        {
            Console.WriteLine("Error: please enter only digits.");
            return 0;
        }

        return count;
    }
}

/*    static async Task GetVideoLinks()
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
    }*/