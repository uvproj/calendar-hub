using System.Globalization;

namespace CalendarCli;

internal static class DateTimeParser
{
    private static readonly string[] EventDateTimeFormats =
    [
        "M/d/yyyy",
        "M/d/yyyy:h:mm tt",
        "M/d/yyyy:H:mm"
    ];

    private static readonly string[] TimeFormats =
    [
        "htt",
        "h tt",
        "h:mmtt",
        "h:mm tt",
        "H:mm"
    ];

    private static readonly (string Phrase, int DaysFromToday)[] RelativeDates =
    [
        ("next week", 7),
        ("tomorrow", 1),
        ("today", 0)
    ];

    public static bool TryParse(string value, out DateTimeOffset result)
        => TryParse(value, DateTimeOffset.Now, out result);

    internal static bool TryParse(string value, DateTimeOffset now, out DateTimeOffset result)
    {
        if (TryParseRelative(value, now, out result))
        {
            return true;
        }

        return DateTimeOffset.TryParseExact(
                   value,
                   EventDateTimeFormats,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeLocal,
                   out result) ||
               DateTimeOffset.TryParse(
                   value,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeLocal,
                   out result);
    }

    public static bool TryParseMonth(string value, out DateTime result) =>
        DateTime.TryParseExact(
            value,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);

    public static string Format(DateTimeOffset value) =>
        value.ToLocalTime().ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture);

    private static bool TryParseRelative(string value, DateTimeOffset now, out DateTimeOffset result)
    {
        var normalizedValue = value.Trim();

        foreach (var (phrase, daysFromToday) in RelativeDates)
        {
            if (!normalizedValue.Equals(phrase, StringComparison.OrdinalIgnoreCase) &&
                !normalizedValue.StartsWith($"{phrase} ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var timeText = normalizedValue[phrase.Length..].Trim();
            var timeOfDay = TimeSpan.Zero;
            var parsedTime = default(DateTime);

            if (timeText.Length > 0 &&
                !DateTime.TryParseExact(
                    timeText,
                    TimeFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out parsedTime))
            {
                result = default;
                return false;
            }

            if (timeText.Length > 0)
            {
                timeOfDay = parsedTime.TimeOfDay;
            }

            var relativeDateTime = now.Date.AddDays(daysFromToday).Add(timeOfDay);
            result = new DateTimeOffset(relativeDateTime, now.Offset);
            return true;
        }

        result = default;
        return false;
    }
}