namespace ABSmartly.Time.Internal;

internal class FixedClock : Clock
{
    private readonly long _millis;

    public FixedClock(long millis)
    {
        _millis = millis;
    }

    public override long Millis()
    {
        return _millis;
    }
}