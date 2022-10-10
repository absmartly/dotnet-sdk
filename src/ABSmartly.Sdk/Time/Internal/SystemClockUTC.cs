using System;

namespace ABSmartly.Time.Internal;

internal class SystemClockUtc : Clock
{
    public override long Millis()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}