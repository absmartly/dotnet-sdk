using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.InternalTests;

[TestFixture]
public class ConcurrencyTests
{
    public void ComputeIfAbsentRW()
    {
        var map = new Mock<Dictionary<int, int>>();
        var computer = new Mock<Func<int, int>>();
        var rlock = new Mock<ReaderWriterLockSlim>();
        var wlock = new Mock<ReaderWriterLockSlim>();
        var rwlock = new Mock<ReaderWriterLockSlim>();

        //rwlock.Setup(p => p.EnterReadLock()).Returns(rlock);
    }
}