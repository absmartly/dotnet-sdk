using ABSmartly.DotNet.Time.Internal;

namespace ABSmartly.DotNet.Time;

public abstract class Clock
{
    public static SystemClockUTC _utc;

    public abstract long Millis();

    public static Clock Fixed(long millis) 
    {
        return new FixedClock(millis);
    }

    public static Clock SystemUTC() 
    {
        if (_utc != null) {
            return _utc;
        }

        return _utc = new SystemClockUTC();
    }


}