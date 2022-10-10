using ABSmartly.Time.Internal;

namespace ABSmartly.Time;

public abstract class Clock
{
    private static SystemClockUtc utc;

    public abstract long Millis();

    public static Clock Fixed(long millis)
    {
        return new FixedClock(millis);
    }

    public static Clock SystemUtc()
    {
        return utc ??= new SystemClockUtc();
    }
}