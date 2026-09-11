namespace Calendar.Core;

internal static class CalendarEventValidation
{
    internal static void ValidateInterval(DateTimeOffset startsAt, DateTimeOffset endsAt, bool isAllDay)
    {
        if (endsAt <= startsAt)
        {
            throw new ArgumentException("The event end must be after its start.", nameof(endsAt));
        }

        if (isAllDay && (startsAt.TimeOfDay != TimeSpan.Zero || endsAt.TimeOfDay != TimeSpan.Zero))
        {
            throw new ArgumentException(
                "All-day event boundaries must be at midnight and the end date is exclusive.",
                nameof(endsAt));
        }
    }
}