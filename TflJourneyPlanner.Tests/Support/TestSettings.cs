using System.Globalization;

namespace TflJourneyPlanner.Tests.Support;

/// <summary>
/// Central place for run-time configuration.
/// Every value can be overridden with an environment variable, so the same code runs
/// unchanged on a laptop (e.g. HEADLESS=false to watch it) and in CI.
/// </summary>
public static class TestSettings
{
    /// <summary>Site under test. Override to point at another environment.</summary>
    public static string BaseUrl => Get("BASE_URL", "https://tfl.gov.uk").TrimEnd('/');

    /// <summary>Run without a visible browser window. Defaults to true (CI-friendly).</summary>
    public static bool Headless => bool.Parse(Get("HEADLESS", "true"));

    /// <summary>
    /// Which Chromium-based browser Playwright drives.
    /// "chrome" (default) = the Google Chrome already installed on the machine, so no extra browser download.
    /// "msedge" = Microsoft Edge. "chromium" = Playwright's own bundled Chromium (needs "playwright install").
    /// </summary>
    public static string? BrowserChannel
    {
        get
        {
            var channel = Get("BROWSER_CHANNEL", "chrome");
            return channel.Equals("chromium", StringComparison.OrdinalIgnoreCase) ? null : channel;
        }
    }

    /// <summary>Slows every action down by N ms. Useful when demoing or debugging; 0 in normal runs.</summary>
    public static float SlowMoMs => float.Parse(Get("SLOW_MO_MS", "0"), CultureInfo.InvariantCulture);

    /// <summary>Max time Playwright waits for an element or an assertion before failing.</summary>
    public static float DefaultTimeoutMs => float.Parse(Get("DEFAULT_TIMEOUT_MS", "15000"), CultureInfo.InvariantCulture);

    /// <summary>
    /// Longer wait used only for journey search results. The results page loads first and then
    /// fetches the journeys in a separate background request, which was observed to take
    /// well over 15 seconds at times on the live site.
    /// </summary>
    public static float ResultsTimeoutMs => float.Parse(Get("RESULTS_TIMEOUT_MS", "45000"), CultureInfo.InvariantCulture);

    private static string Get(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
