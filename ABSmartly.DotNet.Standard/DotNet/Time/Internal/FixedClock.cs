namespace ABSmartly.DotNet.Time.Internal;

public class FixedClock : Clock
{
    protected long _millis;

    public FixedClock(long millis)
    {
        _millis = millis;
    }

    public override long Millis()
    {
        return _millis;;
    }
}