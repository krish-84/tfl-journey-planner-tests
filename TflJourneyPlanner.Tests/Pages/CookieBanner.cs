using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace TflJourneyPlanner.Tests.Pages;

/// <summary>
/// The cookie consent banner covers the whole page (a full-screen overlay) on a first visit,
/// so nothing underneath can be clicked until it is dismissed.
/// We choose "Accept only essential cookies" - the most privacy-preserving option, and it keeps
/// third-party/advertising scripts out of the test run, which makes the page lighter and more stable.
/// </summary>
public sealed class CookieBanner
{
    private readonly IPage _page;

    public CookieBanner(IPage page) => _page = page;

    private ILocator AcceptEssentialOnlyButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Accept only essential cookies", Exact = true });

    private ILocator Overlay => _page.Locator("#cb-cookieoverlay");

    public async Task AcceptEssentialCookiesIfShownAsync()
    {
        try
        {
            // The banner is injected by a script shortly after load, so give it a moment to appear.
            await AcceptEssentialOnlyButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            return; // No banner (e.g. consent already stored) - nothing to do.
        }

        await AcceptEssentialOnlyButton.ClickAsync();

        try
        {
            await Expect(Overlay).ToBeHiddenAsync(new() { Timeout = 5000 });
        }
        catch (PlaywrightException)
        {
            // Site defect (see README "Observations"): intermittently the banner closes but its
            // full-screen overlay stays, blocking every click until the page is refreshed. The
            // consent choice HAS been saved at this point, so a reload loads the page without
            // the banner - the same thing a real user would have to do.
            await _page.ReloadAsync();
            await Expect(Overlay).ToBeHiddenAsync();
        }
    }
}
