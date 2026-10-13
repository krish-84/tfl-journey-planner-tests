using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NUnit.Framework;
using TflJourneyPlanner.Tests.Support;
using static Microsoft.Playwright.Assertions;

namespace TflJourneyPlanner.Tests.Pages;
public sealed class JourneyResultsPage
{
    private readonly IPage _page;

    public JourneyResultsPage(IPage page) => _page = page;

    // The journeys arrive via a separate background request after the page loads, which can be slow
    // on the live site - so waits for search results get a longer timeout than everything else.
    private static LocatorAssertionsToBeVisibleOptions ResultsTimeout => new() { Timeout = TestSettings.ResultsTimeoutMs };

    // Any of these appearing means the background search has finished, whatever the outcome.
    private ILocator AnySearchOutcome => _page.Locator(
        ".journey-results .journey-option:visible, .journey-box:visible, .field-validation-error:visible");

    /// <summary>
    /// Site defect (README "Observations", item 6): intermittently the results page loads but never
    /// sends its background request, so it stays blank - no journey summary, no journeys, no message.
    /// Waiting longer doesn't help, because the trace shows no request is ever made. If the expected
    /// content hasn't appeared by half the results timeout, we log a warning and reload ONCE (the URL
    /// holds the search, so a reload re-runs it). This helper is the framework's only retry for the
    /// results page, and it never retries silently.
    /// </summary>
    private async Task ReloadOnceIfNotShownAsync(ILocator expected, string description)
    {
        try
        {
            await Expect(expected).ToBeVisibleAsync(new() { Timeout = TestSettings.ResultsTimeoutMs / 2 });
        }
        catch (PlaywrightException)
        {
            TestContext.Out.WriteLine(
                $"WARNING: results page showed no {description} - known TfL glitch (README observation 6). Reloading once.");
            await _page.ReloadAsync();
            await Expect(expected).ToBeVisibleAsync(ResultsTimeout);
        }
    }

    private async Task WaitForSearchToFinishAsync() => await ReloadOnceIfNotShownAsync(AnySearchOutcome.First, "search outcome");

    // ---- Locators -------------------------------------------------------------------------

    private ILocator JourneySummary => _page.Locator(".journey-result-summary");

    // "Walking and cycling" panel: one box per mode, each with a time such as "6mins".
    private ILocator WalkingTime => _page.Locator(".journey-box.walking .journey-info");
    private ILocator CyclingTime => _page.Locator(".journey-box.cycling .journey-info");
    private ILocator CyclingRouteData => _page.Locator(".journey-box.cycling .route-data");

    // Walking-only results page (after changing walking speed): one option with distance + time.
    private ILocator WalkingOnlyTime =>
        _page.Locator(".journey-results .journey-option .cycle-walking-journey-info-group.right");
    private ILocator WalkingPreferenceSummary => _page.Locator(".travelpreferences-walking"); // e.g. "Slow walking speed"

    // Public transport options ("Option 1", "Option 2", ...).
    private ILocator JourneyOptions => _page.Locator(".journey-results .journey-option");

    private ILocator EditPreferencesButton => _page.GetByRole(AriaRole.Button, new() { Name = "Edit preferences" });
    private ILocator LeastWalkingOption => _page.Locator("label[for='JourneyPreference_2']");
    private ILocator LeastWalkingRadio => _page.Locator("#JourneyPreference_2");

    // Inside "Edit preferences" the walking options sit behind a "Walking" tab (role="tab").
    private ILocator WalkingTab => _page.GetByRole(AriaRole.Tab, new() { Name = "Walking" });
    private ILocator UpdateJourneyButton => _page.GetByRole(AriaRole.Button, new() { Name = "Update journey" });

    // The site shows "no results" messages using the same CSS class as form validation errors.
    private ILocator MessageWithText(string message) =>
        _page.Locator(".field-validation-error").Filter(new() { HasText = message });

    /// <summary>Expanded details panel of option N (1-based), e.g. #option-1-content.</summary>
    private ILocator OptionDetails(int optionNumber) => _page.Locator($"#option-{optionNumber}-content");

    // ---- Waits & checks -------------------------------------------------------------------

    public async Task WaitForResultsAsync()
    {
        await Expect(_page).ToHaveURLAsync(new Regex("/plan-a-journey/results"));
        await ReloadOnceIfNotShownAsync(JourneySummary, "journey summary");
    }

    public async Task ExpectJourneyFromAndToAsync(string from, string to)
    {
        await Expect(JourneySummary).ToContainTextAsync(from);
        await Expect(JourneySummary).ToContainTextAsync(to);
    }
    private ILocator TravelPreferenceSummary => _page.Locator(".travelpreferences-publictransport");
    public async Task ExpectTravelPreferenceShownAsync(string preferenceText) =>
        await Expect(TravelPreferenceSummary).ToContainTextAsync(new[] { preferenceText });

    public async Task ExpectCyclingDistanceAsync(string distance)
    {
        await WaitForSearchToFinishAsync();
        await Expect(CyclingRouteData).ToContainTextAsync($"Distance: {distance}");
    }

    public async Task ExpectWalkingSpeedShownAsync(string speed) =>
        await Expect(WalkingPreferenceSummary).ToHaveTextAsync($"{speed} walking speed");

    // ---- Reading values -------------------------------------------------------------------

    public async Task<int> GetWalkingTimeInMinutesAsync()
    {
        await WaitForSearchToFinishAsync();
        await Expect(WalkingTime).ToBeVisibleAsync(ResultsTimeout); // results load asynchronously - wait for them
        return JourneyTimeParser.ParseDurationInMinutes(await WalkingTime.InnerTextAsync());
    }

    public async Task<int> GetCyclingTimeInMinutesAsync()
    {
        await WaitForSearchToFinishAsync();
        await Expect(CyclingTime).ToBeVisibleAsync(ResultsTimeout);
        return JourneyTimeParser.ParseDurationInMinutes(await CyclingTime.InnerTextAsync());
    }

    /// <summary>Time shown on the walking-only results page, e.g. "9 min" -> 9.</summary>
    public async Task<int> GetWalkingOnlyJourneyMinutesAsync()
    {
        await WaitForSearchToFinishAsync();
        await Expect(WalkingOnlyTime.First).ToBeVisibleAsync(ResultsTimeout);
        return JourneyTimeParser.ParseDurationInMinutes(await WalkingOnlyTime.First.InnerTextAsync());
    }

    public async Task<IReadOnlyList<JourneyOption>> GetJourneyOptionsAsync()
    {
        await WaitForSearchToFinishAsync();
        await Expect(JourneyOptions.First).ToBeVisibleAsync(ResultsTimeout);

        var options = new List<JourneyOption>();
        foreach (var option in await JourneyOptions.AllAsync())
        {
            var clockText = await option.Locator(".time-box .time").InnerTextAsync();
            var totalText = await option.Locator(".journey-time").InnerTextAsync();

            var (departure, arrival) = JourneyTimeParser.ParseDepartureAndArrival(clockText);
            options.Add(new JourneyOption(departure, arrival, JourneyTimeParser.ParseDurationInMinutes(totalText)));
        }

        return options;
    }
    public async Task<IReadOnlyList<string>> GetAccessInformationAsync(int optionNumber, string stationName)
    {
        // Exact match on the station name, so "Leicester Square" doesn't also match
        // "Leicester Square Underground Station".
        var stationNameExact = new Regex($@"^\s*{Regex.Escape(stationName)}\s*$");

        var step = OptionDetails(optionNumber)
            .Locator(".journey-detail-step")
            .Filter(new() { Has = _page.Locator(".start-location .location-name", new() { HasTextRegex = stationNameExact }) });

        var accessIcons = step.Locator(".access-information a");

        // The icons exist in the HTML even when collapsed, so check they are actually VISIBLE
        // (i.e. "View details" really expanded them), not just present.
        await Expect(accessIcons.First).ToBeVisibleAsync();

        var labels = new List<string>();
        foreach (var icon in await accessIcons.AllAsync())
        {
            labels.Add(await icon.GetAttributeAsync("aria-label") ?? string.Empty);
        }

        return labels;
    }

    public async Task SelectRoutesWithLeastWalkingAsync()
    {
        await EditPreferencesButton.ClickAsync();
        await LeastWalkingOption.ClickAsync();
        await Expect(LeastWalkingRadio).ToBeCheckedAsync();
    }

    /// <summary>
    /// Edit preferences -> "Walking" tab -> Slow / Average / Fast -> Update journey.
    /// The site then shows a walking-only results page for that speed.
    /// </summary>
    public async Task ChangeWalkingSpeedAsync(string speed)
    {
        await EditPreferencesButton.ClickAsync();
        await WalkingTab.ClickAsync();
        await _page.Locator("label[for^='WalkingSpeedWalking_']")
            .Filter(new() { HasTextRegex = new Regex($@"^\s*{Regex.Escape(speed)}\s*$") })
            .ClickAsync();
        await Expect(_page.Locator($"input[name='WalkingSpeedWalking'][value='{speed.ToLowerInvariant()}']")).ToBeCheckedAsync();
        await UpdateJourneyAsync();
    }

    public async Task UpdateJourneyAsync()
    {
        // We are already on a /results URL, so "wait for the results URL" would pass instantly.
        // Instead, wait until the URL actually CHANGES - proof the updated search was submitted.
        var urlBefore = _page.Url;
        await UpdateJourneyButton.ClickAsync();
        await _page.WaitForURLAsync(url => url != urlBefore);
        await WaitForResultsAsync();
    }

    public async Task ViewDetailsAsync(int optionNumber)
    {
        var details = OptionDetails(optionNumber);
        // NOTE: GetByRole cannot be used here. The site marks this visible, clickable button with
        // aria-hidden="true", which removes it from the accessibility tree that GetByRole searches
        // (and hides it from screen readers - see "Observations" in the README). So we fall back
        // to its CSS class and check the visible text instead.
        await WaitForSearchToFinishAsync();
        var viewDetails = details.Locator("button.view-hide-details");
        await Expect(viewDetails).ToHaveTextAsync("View details", new() { Timeout = TestSettings.ResultsTimeoutMs });
        await viewDetails.ClickAsync();

        // The same button's label flips to "Hide details" once the panel is open.
        await Expect(viewDetails).ToHaveTextAsync("Hide details");
    }

    public async Task ExpectNoJourneyResultsAsync(string expectedMessage)
    {
        // Wait for the message FIRST. Results load asynchronously, so asserting "zero options"
        // straight away would pass before the search had even finished - a false positive.
        await WaitForSearchToFinishAsync();
        await Expect(MessageWithText(expectedMessage).First).ToBeVisibleAsync(ResultsTimeout);
        await Expect(JourneyOptions).ToHaveCountAsync(0);
        await Expect(WalkingTime).ToHaveCountAsync(0);
    }
}
