using ABSmartly.Concurrency;

namespace ABSmartly.Sdk.Tests.Concurrency;

[TestFixture]
public class ListLockableAdapterTests
{
    [Test]
    public void TestConcurrentGetOrAdd()
    {
        var lockSlim = Mock.Of<ILockableCollectionSlimLock>();
        var adapter = new ListLockableAdapter<int>(lockSlim);

        adapter.ConcurrentAdd(5);

        adapter[0].Should().Be(5);

        Mock.Get(lockSlim).Verify(x => x.EnterReadLock(), Times.Never);
        Mock.Get(lockSlim).Verify(x => x.ExitReadLock(), Times.Never);
        Mock.Get(lockSlim).Verify(x => x.EnterWriteLock(), Times.Once);
        Mock.Get(lockSlim).Verify(x => x.ExitWriteLock(), Times.Once);
    }
}