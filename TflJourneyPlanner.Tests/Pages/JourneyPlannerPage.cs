using Microsoft.Playwright;
using TflJourneyPlanner.Tests.Support;
using static Microsoft.Playwright.Assertions;

namespace TflJourneyPlanner.Tests.Pages;

public sealed class JourneyPlannerPage
{
    private readonly IPage _page;
    private readonly CookieBanner _cookieBanner;

    public JourneyPlannerPage(IPage page, CookieBanner cookieBanner)
    {
        _page = page;
        _cookieBanner = cookieBanner;
    }

    private ILocator FromInput => _page.Locator("#InputFrom");
    private ILocator ToInput => _page.Locator("#InputTo");
    private ILocator PlanMyJourneyButton => _page.GetByRole(AriaRole.Button, new() { Name = "Plan my journey" });

    public ILocator FromFieldError => _page.Locator("#InputFrom-error");
    public ILocator ToFieldError => _page.Locator("#InputTo-error");

    // The autocomplete list for each field. Each suggestion is an element with role="option".
    private ILocator SuggestionsFor(string inputId) =>
        _page.Locator($"#{inputId}-dropdown").GetByRole(AriaRole.Option);

    // ---- Actions --------------------------------------------------------------------------

    public async Task OpenAsync()
    {
        await _page.GotoAsync($"{TestSettings.BaseUrl}/plan-a-journey/");
        await _cookieBanner.AcceptEssentialCookiesIfShownAsync();
        await Expect(FromInput).ToBeVisibleAsync();
    }

    public async Task SelectFromLocationAsync(string searchText, string suggestion) =>
        await SelectFromSuggestionsAsync(FromInput, "InputFrom", searchText, suggestion);

    public async Task SelectToLocationAsync(string searchText, string suggestion) =>
        await SelectFromSuggestionsAsync(ToInput, "InputTo", searchText, suggestion);

    public async Task EnterFromTextAsync(string text) => await EnterTextWithoutSelectingAsync(FromInput, "InputFrom", text);

    public async Task EnterToTextAsync(string text) => await EnterTextWithoutSelectingAsync(ToInput, "InputTo", text);

    public async Task PlanJourneyAsync() => await PlanMyJourneyButton.ClickAsync();

    public async Task ExpectBothLocationsAreEmptyAsync()
    {
        await Expect(FromInput).ToBeEmptyAsync();
        await Expect(ToInput).ToBeEmptyAsync();
    }

    public async Task ExpectStillOnPlannerPageAsync()
    {
        await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/plan-a-journey/?$"));
        await Expect(PlanMyJourneyButton).ToBeVisibleAsync();
    }

    private async Task SelectFromSuggestionsAsync(ILocator input, string inputId, string searchText, string suggestion)
    {
        await input.ClickAsync();

        // PressSequentially sends real key presses one at a time. The autocomplete listens for
        // key events, so Fill() (which pastes the whole value in one go) may not trigger it.
        await input.PressSequentiallyAsync(searchText, new() { Delay = 50 });

        var option = SuggestionsFor(inputId).Filter(new() { HasText = suggestion }).First;
        await Expect(option).ToHaveTextAsync(suggestion); // exact text - avoids picking a lookalike
        await option.ClickAsync();

        // Prove the selection actually landed in the field.
        await Expect(input).ToHaveValueAsync(suggestion);
    }

    private async Task EnterTextWithoutSelectingAsync(ILocator input, string inputId, string text)
    {
        await input.FillAsync(text);

        // Even for nonsense text the widget can open a list of popular stations (Stratford,
        // Liverpool Street...) that sits ON TOP of the "Plan my journey" button and swallows the
        // click. Whether it appears depends on timing, so the test was flaky. Escape closes it
        // the way a keyboard user would; we then PROVE it is closed and our text is unchanged.
        await input.PressAsync("Escape");
        await Expect(_page.Locator($"#{inputId}-dropdown")).ToBeHiddenAsync();
        await Expect(input).ToHaveValueAsync(text);
    }
}
