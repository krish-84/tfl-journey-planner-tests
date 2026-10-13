using System.Globalization;
using System.Text.RegularExpressions;

namespace TflJourneyPlanner.Tests.Support;

    /// <summary>
    /// Minutes between departure and arrival, read off the clock times.
    /// TimeOnly subtraction works on a 24-hour circular clock, so 23:55 -> 00:10 correctly gives 15.
    /// </summary>
public sealed record JourneyOption(TimeOnly Departure, TimeOnly Arrival, int TotalMinutes)
{
            public int ClockMinutes => (int)(Arrival - Departure).TotalMinutes;
}

    /// <summary>
    /// Turns the text the website displays into numbers we can assert on.
    /// Kept separate from the page objects so it has one clear job: text in, numbers out.
    /// </summary>
public static partial class JourneyTimeParser
{
    // Matches e.g. "6mins", "1min", "Total time: 11mins", "1h 5mins", "2hrs".
    [GeneratedRegex(@"(?:(?<hours>\d+)\s*h(?:ou)?r?s?\b)?\s*(?:(?<minutes>\d+)\s*mins?\b)?", RegexOptions.IgnoreCase)]
    private static partial Regex DurationRegex();

    // Matches clock times such as "09:41" or "11:07".
    [GeneratedRegex(@"\b(\d{1,2}:\d{2})\b")]
    private static partial Regex ClockTimeRegex();

    /// <summary>"Total time: 1h 5mins" -> 65. Throws if no duration is found.</summary>
    public static int ParseDurationInMinutes(string text)
    {
        foreach (Match match in DurationRegex().Matches(text))
        {
            var hours = match.Groups["hours"].Success ? int.Parse(match.Groups["hours"].Value) : 0;
            var minutes = match.Groups["minutes"].Success ? int.Parse(match.Groups["minutes"].Value) : 0;

            if (match.Groups["hours"].Success || match.Groups["minutes"].Success)
                return hours * 60 + minutes;
        }

        throw new FormatException($"No journey duration found in text: '{text}'");
    }

    /// <summary>"Depart at: 11:07 - Arrive at: 11:18" -> (11:07, 11:18).</summary>
    public static (TimeOnly Departure, TimeOnly Arrival) ParseDepartureAndArrival(string text)
    {
        var times = ClockTimeRegex().Matches(text);
        if (times.Count < 2)
            throw new FormatException($"Expected a departure and an arrival time in text: '{text}'");

        return (ParseClock(times[0].Value), ParseClock(times[1].Value));
    }

    private static TimeOnly ParseClock(string value) =>
        TimeOnly.ParseExact(value.PadLeft(5, '0'), "HH:mm", CultureInfo.InvariantCulture);
}
