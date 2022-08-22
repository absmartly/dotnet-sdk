using ABSmartlySdk.Internal;
using ABSmartlySdk.Utils;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests.InternalTests;

[TestFixture]
public class ConcurrencyTests
{
    [Test]
    public void ComputeIfAbsentRW()
    {
        // Arrange
        var rwlock = new Mock<ABLock>();
        var map = new Mock<IDictionary<int, int>>();
        var computer = new Mock<Func<int, int>>();

        computer.Setup(p => p.Invoke(1)).Returns(5);

        // Act
        var result = Concurrency.ComputeIfAbsentRW(rwlock.Object, map.Object, 1, computer.Object);

        // Assert
        Assert.That(result, Is.EqualTo(5));

        rwlock.Verify(ablock => ablock.EnterReadLock(), Times.Exactly(1));
        rwlock.Verify(ablock => ablock.ExitReadLock(), Times.Exactly(1));

        rwlock.Verify(ablock => ablock.EnterWriteLock(), Times.Exactly(1));
        rwlock.Verify(ablock => ablock.ExitWriteLock(), Times.Exactly(1));

        map.Verify(m => m.ContainsKey(It.IsAny<int>()), Times.Exactly(2));
        map.Verify(m => m.ContainsKey(1), Times.Exactly(2));

        computer.Verify(c => c.Invoke(It.IsAny<int>()) , Times.Exactly(1));
        computer.Verify(c => c.Invoke(1), Times.Exactly(1));
    }

    [Test]
    public void ComputeIfAbsentRWPresent()
    {
        // Arrange
        var rwlock = new Mock<ABLock>();
        var map = new Mock<IDictionary<int, int>>();
        var computer = new Mock<Func<int, int>>();

        map.Setup(p => p.ContainsKey(1)).Returns(true);
        map.Setup(p => p[1]).Returns(5);

        // Act
        var result = Concurrency.ComputeIfAbsentRW(rwlock.Object, map.Object, 1, computer.Object);

        // Assert
        Assert.That(result, Is.EqualTo(5));

        rwlock.Verify(ablock => ablock.EnterReadLock(), Times.Exactly(1));
        rwlock.Verify(ablock => ablock.ExitReadLock(), Times.Exactly(1));

        rwlock.Verify(ablock => ablock.EnterWriteLock(), Times.Exactly(0));
        rwlock.Verify(ablock => ablock.ExitWriteLock(), Times.Exactly(0));

        map.Verify(m => m.ContainsKey(It.IsAny<int>()), Times.Exactly(1));
        map.Verify(m => m.ContainsKey(1), Times.Exactly(1));

        computer.Verify(func => func.Invoke(It.IsAny<int>()) , Times.Exactly(0));
    }

    [Test]
    public void ComputeIfAbsentRWPresentAfterLock()
    {
        // Arrange
        var rwlock = new Mock<ABLock>();
        var map = new Mock<IDictionary<int, int>>();
        var computer = new Mock<Func<int, int>>();

        rwlock.Setup(ablock => ablock.EnterWriteLock()).Callback(() =>
        {
            map.Setup(p => p.ContainsKey(1)).Returns(true);
            map.Setup(p => p[1]).Returns(5);
        });

        // Act
        var result = Concurrency.ComputeIfAbsentRW(rwlock.Object, map.Object, 1, computer.Object);

        // Assert
        Assert.That(result, Is.EqualTo(5));

        rwlock.Verify(ablock => ablock.EnterReadLock(), Times.Exactly(1));
        rwlock.Verify(ablock => ablock.ExitReadLock(), Times.Exactly(1));

        rwlock.Verify(ablock => ablock.EnterWriteLock(), Times.Exactly(1));
        rwlock.Verify(ablock => ablock.ExitWriteLock(), Times.Exactly(1));

        map.Verify(m => m.ContainsKey(It.IsAny<int>()), Times.Exactly(2));
        map.Verify(m => m.ContainsKey(1), Times.Exactly(2));

        computer.Verify(func => func.Invoke(It.IsAny<int>()) , Times.Exactly(0));
    }

    [Test]
    public void GetRW_EmptyMap()
    {
        // Arrange
        var rwlock = new Mock<ABLock>();
        //var map = new Mock<IDictionary<int, int>>();
        var map = new Mock<IDictionary<int, int?>>();

        // Act
        var result = Concurrency.GetRW(rwlock.Object, map.Object, 1);

        // Assert
        Assert.IsNull(result);

        rwlock.Verify(ablock => ablock.EnterReadLock(), Times.Exactly(1));
        rwlock.Verify(ablock => ablock.ExitReadLock(), Times.Exactly(1));

        rwlock.Verify(ablock => ablock.EnterWriteLock(), Times.Exactly(0));
        rwlock.Verify(ablock => ablock.ExitWriteLock(), Times.Exactly(0));

        map.Verify(m => m.ContainsKey(It.IsAny<int>()), Times.Exactly(1));
        map.Verify(m => m.ContainsKey(1), Times.Exactly(1));
        map.Verify(m => m[It.IsAny<int>()], Times.Exactly(0));
        map.Verify(m => m[1], Times.Exactly(0));
    }

    [Test]
    public void PutRW()
    {
        // Arrange
        var rwlock = new Mock<ABLock>();
        //var map = new Mock<IDictionary<int, int>>();
        var map = new Mock<IDictionary<int, int?>>();

        // Act
        var result = Concurrency.PutRW(rwlock.Object, map.Object, 1, 5);

        // Assert
        Assert.IsNull(result);

        rwlock.Verify(ablock => ablock.EnterReadLock(), Times.Exactly(0));
        rwlock.Verify(ablock => ablock.ExitReadLock(), Times.Exactly(0));

        rwlock.Verify(ablock => ablock.EnterWriteLock(), Times.Exactly(1));
        rwlock.Verify(ablock => ablock.ExitWriteLock(), Times.Exactly(1));

        map.Verify(m => m.Add(It.IsAny<int>(), It.IsAny<int?>()), Times.Exactly(1));
        map.Verify(m => m.Add(1, 5), Times.Exactly(1));
    }

    [Test]
    public void AddRW()
    {
        // Arrange
        var rwlock = new Mock<ABLock>();
        var list = new List<int>();

        // Act
        Concurrency.AddRW(rwlock.Object, list, 3);

        // Assert
        Assert.IsTrue(list.Contains(3));

        rwlock.Verify(ablock => ablock.EnterReadLock(), Times.Exactly(0));
        rwlock.Verify(ablock => ablock.ExitReadLock(), Times.Exactly(0));

        rwlock.Verify(ablock => ablock.EnterWriteLock(), Times.Exactly(1));
        rwlock.Verify(ablock => ablock.ExitWriteLock(), Times.Exactly(1));
    }
}