using ABSmartly.Utils;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.InternalTests;

[TestFixture]
public class ConcurrencyTests
{
    [Test]
    public void ComputeIfAbsentRW()
    {
        // Arrange
        var map = new Mock<Dictionary<int, int>>();
        var computer = new Mock<Func<int, int>>();
        var rwlock = new Mock<ABLock>();

        computer.Setup(p => p.Invoke(1)).Returns(5);

        // Act
        var result = Concurrency.ComputeIfAbsentRW(rwlock.Object, map.Object, 1, computer.Object);

        // Assert
        Assert.That(result, Is.EqualTo(5));

        rwlock.Verify(slim => slim.EnterReadLock(), Times.Exactly(1));
        rwlock.Verify(slim => slim.EnterWriteLock(), Times.Exactly(1));

        computer.Verify(c => c.Invoke(1), Times.Exactly(1));
    }
}