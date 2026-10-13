using NUnit.Framework;
using Reqnroll;
using TflJourneyPlanner.Tests.Pages;
using static Microsoft.Playwright.Assertions;

namespace TflJourneyPlanner.Tests.StepDefinitions;

[Binding]
public sealed class JourneyPlannerSteps
{
    private readonly JourneyPlannerPage _planner;
    private readonly JourneyResultsPage _results;
    private int _openedOption = 1;

    public JourneyPlannerSteps(JourneyPlannerPage planner, JourneyResultsPage results)
    {
        _planner = planner;
        _results = results;
    }

    // ---- Given ----------------------------------------------------------------------------

    [Given("I am on the TfL Plan a journey page")]
    public async Task GivenIAmOnThePlanAJourneyPage() => await _planner.OpenAsync();

    [Given("I have planned a journey with:")]
    public async Task GivenIHavePlannedAJourneyWith(DataTable journey)
    {
        foreach (var row in journey.Rows)
        {
            var searchText = row["Search text"];
            var suggestion = row["Suggestion"];

            switch (row["Field"])
            {
                case "From": await _planner.SelectFromLocationAsync(searchText, suggestion); break;
                case "To": await _planner.SelectToLocationAsync(searchText, suggestion); break;
                default: throw new ArgumentException($"Unknown field '{row["Field"]}'. Use From or To.");
            }
        }

        await _planner.PlanJourneyAsync();
        await _results.WaitForResultsAsync();
    }

    [Given("I have updated my preferences to show routes with least walking")]
    public async Task GivenIHaveUpdatedMyPreferencesToLeastWalking()
    {
        await _results.SelectRoutesWithLeastWalkingAsync();
        await _results.UpdateJourneyAsync();
    }

    [Given("I have not entered a From or To location")]
    public async Task GivenIHaveNotEnteredAnyLocation() => await _planner.ExpectBothLocationsAreEmptyAsync();

    // ---- When -----------------------------------------------------------------------------

    [When("I type {string} in the From field and select {string} from the suggestions")]
    public async Task WhenISelectTheFromLocation(string searchText, string suggestion) =>
        await _planner.SelectFromLocationAsync(searchText, suggestion);

    [When("I type {string} in the To field and select {string} from the suggestions")]
    public async Task WhenISelectTheToLocation(string searchText, string suggestion) =>
        await _planner.SelectToLocationAsync(searchText, suggestion);

    [When("I enter {string} in the From field without selecting a suggestion")]
    public async Task WhenIEnterFromText(string text) => await _planner.EnterFromTextAsync(text);

    [When("I enter {string} in the To field without selecting a suggestion")]
    public async Task WhenIEnterToText(string text) => await _planner.EnterToTextAsync(text);

    [When("I plan my journey")]
    public async Task WhenIPlanMyJourney() => await _planner.PlanJourneyAsync();

    [When("I edit my preferences to show routes with least walking")]
    public async Task WhenIEditMyPreferencesToLeastWalking() => await _results.SelectRoutesWithLeastWalkingAsync();

    [When("I update the journey")]
    public async Task WhenIUpdateTheJourney() => await _results.UpdateJourneyAsync();

    [When("I view the details of journey option {int}")]
    public async Task WhenIViewTheDetailsOfOption(int optionNumber)
    {
        _openedOption = optionNumber; // remembered so the Then step checks the same option
        await _results.ViewDetailsAsync(optionNumber);
    }

    // ---- Then: valid journey --------------------------------------------------------------

    [Then("journey results are shown from {string} to {string}")]
    public async Task ThenJourneyResultsAreShown(string from, string to)
    {
        await _results.WaitForResultsAsync();
        await _results.ExpectJourneyFromAndToAsync(from, to);
    }

    [Then("the walking time is {int} minutes")]
    public async Task ThenTheWalkingTimeIs(int expectedMinutes)
    {
        Assert.That(await _results.GetWalkingTimeInMinutesAsync(), Is.EqualTo(expectedMinutes), "Walking time (minutes)");
    }

    [Then("the cycling time is {int} minute(s)")]
    public async Task ThenTheCyclingTimeIs(int expectedMinutes)
    {
        Assert.That(await _results.GetCyclingTimeInMinutesAsync(), Is.EqualTo(expectedMinutes), "Cycling time (minutes)");
    }

    [Then("the cycling distance is {string}")]
    public async Task ThenTheCyclingDistanceIs(string distance) => await _results.ExpectCyclingDistanceAsync(distance);

    // ---- When/Then: walking speed ---------------------------------------------------------

    [When("I change my walking speed to {string}")]
    public async Task WhenIChangeMyWalkingSpeed(string speed) => await _results.ChangeWalkingSpeedAsync(speed);

    [Then("the results are for a {string} walking speed")]
    public async Task ThenTheResultsAreForWalkingSpeed(string speed) => await _results.ExpectWalkingSpeedShownAsync(speed);

    [Then("the walking journey takes {int} minutes")]
    public async Task ThenTheWalkingJourneyTakes(int expectedMinutes)
    {
        Assert.That(await _results.GetWalkingOnlyJourneyMinutesAsync(), Is.EqualTo(expectedMinutes), "Walking-only journey time (minutes)");
    }

    // ---- Then: preferences ----------------------------------------------------------------

    [Then("the results are showing {string}")]
    public async Task ThenTheResultsAreShowing(string preferenceText) =>
        await _results.ExpectTravelPreferenceShownAsync(preferenceText);

    [Then("every journey option has a total time that matches its departure and arrival times")]
    public async Task ThenTotalTimesMatchTheClockTimes()
    {
        var options = await _results.GetJourneyOptionsAsync();
        Assert.That(options, Is.Not.Empty, "Expected at least one journey option");

        // Assert.Multiple reports EVERY mismatching option, not just the first one.
        Assert.Multiple(() =>
        {
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                TestContext.Out.WriteLine($"Option {i + 1}: {option.Departure:HH:mm} -> {option.Arrival:HH:mm}, total {option.TotalMinutes} mins");

                Assert.That(option.TotalMinutes, Is.GreaterThan(0), $"Option {i + 1}: total time should be positive");
                Assert.That(option.TotalMinutes, Is.EqualTo(option.ClockMinutes),
                    $"Option {i + 1}: total time ({option.TotalMinutes} mins) does not match " +
                    $"{option.Departure:HH:mm} -> {option.Arrival:HH:mm} ({option.ClockMinutes} mins)");
            }
        });
    }

    [Then("every journey option takes less than {int} minutes")]
    public async Task ThenEveryOptionTakesLessThan(int limit)
    {
        var options = await _results.GetJourneyOptionsAsync();
        Assert.That(options.Select(o => o.TotalMinutes), Is.All.LessThan(limit));
    }

    // ---- Then: access information ---------------------------------------------------------

    [Then("the access information at {string} is:")]
    public async Task ThenTheAccessInformationIs(string stationName, DataTable expected)
    {
        var expectedLabels = expected.Rows.Select(row => row["Access information"]).ToList();
        var actualLabels = await _results.GetAccessInformationAsync(_openedOption, stationName);

        Assert.That(actualLabels, Is.EqualTo(expectedLabels),
            $"Access information at {stationName} - expected [{string.Join(", ", expectedLabels)}] " +
            $"but was [{string.Join(", ", actualLabels)}]");
    }

    // ---- Then: negative paths -------------------------------------------------------------

    [Then("no journey results are shown and the message {string} is displayed")]
    public async Task ThenNoJourneyResultsAreShown(string message) => await _results.ExpectNoJourneyResultsAsync(message);

    [Then("the From field shows the error {string}")]
    public async Task ThenTheFromFieldShowsTheError(string error) => await Expect(_planner.FromFieldError).ToHaveTextAsync(error);

    [Then("the To field shows the error {string}")]
    public async Task ThenTheToFieldShowsTheError(string error) => await Expect(_planner.ToFieldError).ToHaveTextAsync(error);

    [Then("I remain on the Plan a journey page")]
    public async Task ThenIRemainOnThePlannerPage() => await _planner.ExpectStillOnPlannerPageAsync();
}
