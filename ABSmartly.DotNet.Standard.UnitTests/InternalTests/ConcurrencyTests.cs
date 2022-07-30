using ABSmartly.Utils;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.InternalTests;

[TestFixture]
public class ConcurrencyTests
{
    [Test]
    public void ComputeIfAbsentRW()
    {
        var map = new Mock<Dictionary<int, int>>();
        var computer = new Mock<Func<int, int>>();
        var rwlock = new Mock<ABLock>();
        //var rwlock = new ReaderWriterLockSlim();
        

        //int rlockk = 0;
        //int wlockk = 0;
        //var rlock = new Mock<ReaderWriterLockSlim>();
        //var wlock = new Mock<ReaderWriterLockSlim>();

        //rwlock.Setup((slim => slim.EnterReadLock()));
        //rwlock.Setup(p => p.EnterReadLock()).Raises(_ => rlockk++);

        //rwlock.Setup(p => p.EnterWriteLock()).Callback(() => rlockk++);
        //rwlock.Setup(p => p.EnterWriteLock()).Raises(_ => wlockk++);

        //rwlock.Setup(p => p.EnterReadLock()).Raises(_ => rlock.Object.EnterReadLock());
        //rwlock.Setup(p => p.EnterWriteLock()).Raises(_ => wlock.Object.EnterWriteLock());

        computer.Setup(p => p.Invoke(1)).Returns(5);

        // Act
        var result = Concurrency.ComputeIfAbsentRW(rwlock.Object, map.Object, 1, computer.Object);

        // Assert
        Assert.That(result, Is.EqualTo(5));

        rwlock.Verify(slim => slim.EnterReadLock(), Times.Exactly(2));
    }
}