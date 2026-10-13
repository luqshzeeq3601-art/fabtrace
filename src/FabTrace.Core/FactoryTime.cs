namespace FabTrace.Core;

/// <summary>
/// The database stores UTC. Shifts are defined in factory local time, so this is
/// the one place that knows the factory's offset.
/// </summary>
public static class FactoryTime
{
    /// <summary>Malaysia is UTC+8 all year (no daylight saving).</summary>
    public static readonly TimeSpan UtcOffset = TimeSpan.FromHours(8);

    public static DateTimeOffset ToLocal(DateTimeOffset instant) => instant.ToOffset(UtcOffset);

    public static TimeOnly LocalTimeOfDay(DateTimeOffset instant) => TimeOnly.FromTimeSpan(ToLocal(instant).TimeOfDay);

    /// <summary>
    /// Calculates the UTC start timestamp of the current factory shift.
    /// Morning: 07:00-15:00, Afternoon: 15:00-23:00, Night: 23:00-07:00.
    /// </summary>
    public static DateTime GetCurrentShiftStartUtc(DateTime utcNow)
    {
        var local = utcNow + UtcOffset;
        var localDate = DateOnly.FromDateTime(local);
        var localTime = TimeOnly.FromDateTime(local);

        DateTime shiftStartLocal;
        if (localTime >= new TimeOnly(7, 0) && localTime < new TimeOnly(15, 0))
        {
            shiftStartLocal = localDate.ToDateTime(new TimeOnly(7, 0));
        }
        else if (localTime >= new TimeOnly(15, 0) && localTime < new TimeOnly(23, 0))
        {
            shiftStartLocal = localDate.ToDateTime(new TimeOnly(15, 0));
        }
        else if (localTime >= new TimeOnly(23, 0))
        {
            shiftStartLocal = localDate.ToDateTime(new TimeOnly(23, 0));
        }
        else
        {
            shiftStartLocal = localDate.AddDays(-1).ToDateTime(new TimeOnly(23, 0));
        }

        return DateTime.SpecifyKind(shiftStartLocal - UtcOffset, DateTimeKind.Utc);
    }
}