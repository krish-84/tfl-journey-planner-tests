using Microsoft.Playwright;
using NUnit.Framework;
using Reqnroll;
using Reqnroll.BoDi;
using TflJourneyPlanner.Tests.Support;

namespace TflJourneyPlanner.Tests.Hooks;

/// <summary>
/// Owns the browser lifecycle.
///
///   Test run  -> ONE browser process (starting a browser is slow, so we share it).
///   Scenario  -> a NEW browser context + page (like a fresh incognito window),
///                so no cookies / history leak from one scenario into the next.
///
/// The page is registered in Reqnroll's dependency-injection container, which is how
/// the page objects and step definitions receive it through their constructors.
/// </summary>
[Binding]
public sealed class PlaywrightHooks
{
    private static IPlaywright? _playwright;
    private static IBrowser? _browser;

    private readonly IObjectContainer _container;
    private readonly ScenarioContext _scenarioContext;
    private IBrowserContext? _context;

    public PlaywrightHooks(IObjectContainer container, ScenarioContext scenarioContext)
    {
        _container = container;
        _scenarioContext = scenarioContext;
    }

    [BeforeTestRun]
    public static async Task StartBrowser()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = TestSettings.BrowserChannel,
            Headless = TestSettings.Headless,
            SlowMo = TestSettings.SlowMoMs
        });

        // Web-first assertions (Expect(...)) retry until this timeout before failing.
        Assertions.SetDefaultExpectTimeout(TestSettings.DefaultTimeoutMs);
    }

    [BeforeScenario]
    public async Task OpenFreshPage()
    {
        _context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1366, Height = 900 },
            Locale = "en-GB",
            TimezoneId = "Europe/London" // journey times are London times
        });

        // Record a trace for every scenario; we only save it to disk if the scenario fails.
        await _context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });

        var page = await _context.NewPageAsync();
        page.SetDefaultTimeout(TestSettings.DefaultTimeoutMs);

        _container.RegisterInstanceAs(page);
    }

    [AfterScenario]
    public async Task ClosePage()
    {
        if (_context is null) return;

        if (_scenarioContext.TestError is not null)
        {
            await SaveFailureEvidence();
        }
        else
        {
            await _context.Tracing.StopAsync();
        }

        await _context.CloseAsync();
    }

    [AfterTestRun]
    public static async Task StopBrowser()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    /// <summary>Screenshot + Playwright trace for a failed scenario, attached to the NUnit result.</summary>
    private async Task SaveFailureEvidence()
    {
        var folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts");
        Directory.CreateDirectory(folder);

        var name = SafeFileName(_scenarioContext.ScenarioInfo.Title);
        var screenshotPath = Path.Combine(folder, $"{name}.png");
        var tracePath = Path.Combine(folder, $"{name}-trace.zip");

        try
        {
            var page = _container.Resolve<IPage>();
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
            TestContext.AddTestAttachment(screenshotPath, "Screenshot at failure");
        }
        catch (Exception ex)
        {
            // Never let evidence-gathering hide the real failure.
            TestContext.Out.WriteLine($"Could not take failure screenshot: {ex.Message}");
        }

        await _context!.Tracing.StopAsync(new TracingStopOptions { Path = tracePath });
        TestContext.AddTestAttachment(tracePath, "Playwright trace (open at https://trace.playwright.dev)");
        TestContext.Out.WriteLine($"Failure evidence saved to: {folder}");
    }

    private static string SafeFileName(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(text.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray());
        return $"{cleaned}_{DateTime.Now:yyyyMMdd_HHmmss}";
    }
}
