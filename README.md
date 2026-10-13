# TfL Journey Planner – UI Test Automation Framework

Automated UI tests for the **Plan a journey** widget on [tfl.gov.uk/plan-a-journey](https://tfl.gov.uk/plan-a-journey), written in **C#** with **Gherkin** (Reqnroll), **Playwright for .NET** and **NUnit**.

|               |                                                                              |
| ------------- | ---------------------------------------------------------------------------- |
| Language      | C# / .NET 10                                                                 |
| BDD / Gherkin | [Reqnroll](https://reqnroll.net)                                             |
| UI automation | [Microsoft.Playwright](https://playwright.dev/dotnet/) driving Google Chrome |
| Test runner   | NUnit                                                                        |
| CI            | GitHub Actions (`.github/workflows/ui-tests.yml`)                            |

Additional functional and non-functional scenarios (not automated, as requested) are in **[ADDITIONAL-SCENARIOS.md](ADDITIONAL-SCENARIOS.md)**.

---

## Running the tests

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Google Chrome (installed normally). No separate browser driver is needed (see _Playwright and ChromeDriver_ below).

### Run

```bash
cd TflJourneyPlanner.Tests
dotnet test
```

### Useful options (environment variables)

| Variable             | Default              | Purpose                                                                                        |
| -------------------- | -------------------- | ---------------------------------------------------------------------------------------------- |
| `HEADLESS`           | `true`               | `false` opens a visible browser window, for watching or debugging                              |
| `SLOW_MO_MS`         | `0`                  | Slows every action down, e.g. `500`, useful for demos                                          |
| `BROWSER_CHANNEL`    | `chrome`             | `msedge` for Edge, or `chromium` for Playwright's bundled browser (needs `playwright install`) |
| `BASE_URL`           | `https://tfl.gov.uk` | Point the same tests at another environment                                                    |
| `DEFAULT_TIMEOUT_MS` | `15000`              | Maximum wait for elements and assertions                                                       |
| `RESULTS_TIMEOUT_MS` | `45000`              | Longer maximum wait for journey search results only (see decision 6)                           |

```bash
# Watch the tests run
HEADLESS=false SLOW_MO_MS=300 dotnet test TflJourneyPlanner.Tests

# Run one group of scenarios by tag
dotnet test TflJourneyPlanner.Tests --filter "Category=smoke"
```

When a scenario fails, a **full-page screenshot** and a **Playwright trace** are saved under `bin/.../artifacts/` and attached to the test result. A trace can be opened at [trace.playwright.dev](https://trace.playwright.dev) to step through every action, network call and DOM snapshot.

---

## What is covered

All scenarios are in [`Features/JourneyPlanner.feature`](TflJourneyPlanner.Tests/Features/JourneyPlanner.feature)

| #   | Scenario                                                        | What it checks                                                                                                                                                                                                           |
| --- | --------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1   | Valid journey: Leicester Square → Covent Garden                 | Locations are picked from the **autocomplete suggestions** (partial text typed). Walking time is **6 minutes** (average speed) and cycling is **1 minute over 0.4 km** ("cycle directly to my destination", the default) |
| 1b  | Walking speed (Scenario Outline × 3)                            | Changing the walking speed gives **Slow 9, Average 6, Fast 5 minutes**, and the results confirm the speed used                                                                                                           |
| 2   | Edit preferences → _routes with least walking_ → Update journey | The preference is applied, and every option's **total time equals arrival minus departure**, and every option takes **less than 20 minutes**                                                                             |
| 3   | View details                                                    | The access information at **Covent Garden Underground Station** is exactly _Up stairs, Up lift, Level walkway_ and is actually visible                                                                                   |
| 4   | Invalid journey (both invalid, and one invalid)                 | No journey options and no walking/cycling times are shown, and a "no results" message is displayed                                                                                                                       |
| 5   | No locations entered                                            | "The From field is required." / "The To field is required." are shown and the user stays on the planner page                                                                                                             |

---

## Project structure

```
TflJourneyPlanner.Tests/
├── Features/            Gherkin feature file: the "what", in business language
├── StepDefinitions/     Thin glue from Gherkin steps to page objects and assertions
├── Pages/               Page objects: the only place that knows selectors
│   ├── JourneyPlannerPage.cs
│   ├── JourneyResultsPage.cs
│   └── CookieBanner.cs
├── Hooks/               Browser lifecycle, failure screenshots and traces
└── Support/             Settings (env vars) and text-to-number parsing
```

---

## Development decisions

**1. Reqnroll for Gherkin, not SpecFlow.** SpecFlow reached end of life in December 2024. Reqnroll is its community-maintained successor, with the same syntax and concepts, so I chose the supported option for a new framework.

**2. Page Object Model with dependency injection.** Steps describe _intent_, and page objects own _how_. If TfL changes its HTML, only one file changes. Reqnroll's built-in container gives each scenario its own page objects through constructors, with no static or shared state.

**3. One browser per run, a fresh context per scenario.** Launching a browser is slow, so it is shared across the run. Each scenario gets a new **browser context** (like a fresh incognito window), so cookies and history never leak between tests, and any scenario can run on its own, in any order.

**4. Independent scenarios.** Scenarios 2 and 3 build on a planned journey, but each one sets up its own state (`Given I have planned a journey with: …`) instead of depending on an earlier scenario having run.

**5. Locator strategy.** In order of preference: accessible **role + name** for buttons and options (`GetByRole(Button, "Update journey")`), which is what a user or screen reader sees; then stable **ids** the site provides (`#InputFrom`, `#option-1-content`); and only then descriptive CSS classes where nothing better exists (`.journey-box.walking`). There are no XPath or position-based selectors. The one exception to role-based lookup is the "View details" button: the site hides it from the accessibility tree (see _Observations_, item 4).

**6. No fixed sleeps.** Every wait uses Playwright's auto-waiting and web-first assertions (`Expect(...).ToBeVisibleAsync()`), which retry until the condition holds or time out. Two places needed extra care:

- **Negative results load asynchronously**, so asserting "0 options" straight away would pass _before the search finished_ (a false positive). The test waits for the "no results" message first, then asserts the count.
- **Update journey** happens while already on a `/results` URL, so the test waits for the URL to _change_ rather than just match.
- **The journeys arrive in a separate background request** after the results page loads. While exploring the site, that request sometimes took over 15 seconds to start, so waits for search results get their own, longer timeout. Everything else stays at the shorter default, so a genuinely broken page still fails quickly.

**7. Exact values only where the data is stable.** Walking and cycling times are calculated from distance and speed, not live train data, so they are asserted exactly (6, 9, 5 and 1 minutes). Public transport times change with delays and timetables, so for those the tests assert **properties that should always hold**: _total time = arrival − departure_ (which also handles journeys that cross midnight), and an upper limit of 20 minutes. A delay changes the numbers but not those relationships, so the tests catch real defects without failing on normal variation.

**8. Autocomplete is driven like a user.** Partial text is typed key by key (`PressSequentially`), because the autocomplete reacts to key events and a single `Fill` may not trigger it. The suggestion is then clicked, and the field's final value is checked.

**9. Cookie consent.** The banner is a full-screen overlay. The tests choose **"Accept only essential cookies"**: it's the privacy-preserving option, and it keeps advertising scripts out of the run, which makes pages lighter and tests more stable.

**10. Invalid test data.** Symbol-only strings (`@@@###`) are used for invalid locations. Nonsense _letters_ turned out not to be reliably invalid, because the location search fuzzy-matches them (see _Observations_).

**11. Sequential execution.** The tests run against a public production website, so they run one at a time to keep load on the site to a minimum. Fresh contexts per scenario mean parallel execution could be switched on later if needed.

**12. Pinned, current dependencies.** Playwright 1.63.0 and Reqnroll 3.3.4. Reqnroll 3 is a major version upgrade, so it was applied as a separate change and the full suite verified green before moving on. The newest Playwright release was only days old at the time, so a month-old stable release was chosen deliberately. NUnit.Analyzers checks the test code at compile time, and the project builds with zero warnings.

---

## Observations from exploring the widget

These came up while building the tests. I would raise them with the team for discussion; they are not necessarily defects.

1. **Nonsense input can resolve to a real place.** Entering `zzzzqqqqxxxx` as _From_ returned a 59-minute journey starting from a café in Bromley, matched by fuzzy search. A user who mistyped could get a confident but wrong route. A "did you mean…?" step might be safer.
2. **Inconsistent "no results" messaging.** `@@@###` gives _"Journey planner could not find any results to your search. Please try again"_, while `~~~~~~~~` gives _"Sorry, we can't find a journey matching your criteria"_. Consistent wording would help users and make the behaviour easier to specify.
3. **Optimistic cycle time.** For Leicester Square → Covent Garden (0.4 km) the cycling time shown was 1 minute (about 24 km/h, including setting off and stopping), compared with a 6-minute walk. This might be worth checking against the routing assumptions.
4. **Accessibility defect: the "View details" and "Map view" buttons are hidden from assistive technology.** Both are visible, focusable `<button>`s marked `aria-hidden="true"`. Screen reader users can tab to a control that is never announced, which fails WCAG 2.2 SC 4.1.2 (Name, Role, Value) and the axe-core rule `aria-hidden-focus`. My test first located the button by role and name, as a screen reader would, and could not find it. That is how this defect surfaced. The test now locates the button by CSS class instead, with a comment explaining why.
5. **The cookie overlay can stay stuck after consent.** Intermittently, after "Accept only essential cookies" is clicked, the banner disappears but its invisible full-screen overlay stays in place, so the page looks normal but nothing can be clicked until it is refreshed. This showed up in about 2 of 12 runs of the negative scenarios. The tests handle it by reloading once, because the consent choice has already been saved by then, and the handling is commented in `CookieBanner.cs`.
6. **The results page sometimes never starts its search.** Intermittently, the results page loads but the background search request is never sent, so the page stays blank with no journeys and no message. The Playwright trace confirms that no search request was made in 45 seconds. This happens in the browser, not on a slow server, so waiting longer doesn't help, and a real user would have to refresh. These are functional tests, so the framework logs a warning and reloads once (`ReloadOnceIfNotShownAsync` in `JourneyResultsPage.cs`). It has been seen both before the journey summary appears and before the results arrive, so both waits use it. This is the only retry for the results page in the suite. Reliability and performance of the search belong in API and non-functional testing.
7. **"Routes with least walking" involves more walking than walking.** With _least walking_ selected, the suggested route takes 11 minutes and its accessibility details show **10 minutes of walking** (5 minutes to the platform, 1 minute on the Piccadilly line, 5 minutes out). Walking all the way takes **6 minutes**. The algorithm appears to compare only public transport routes with each other. For a user who chose "least walking" because walking is hard for them, that result is misleading.

---

## Use of AI

This project was developed with the help of AI assistant. AI was used to:

- propose the framework structure
- javascript and typescript are the languages that I have worked in the career. So being new to cs had to use a lot of help to set up
- having worked with cucumber ages ago, was slightly out of touch and never have worked with specflow or reqnroll. So again took a lot of help but I, now understand and can explain and also comfortably add more tests
- help taken to write this READ.ME file, especially for the non-functional bit as its an area that I understand but haven't been involved much with
