using ABSmartly.Concurrency;

namespace ABSmartly.Sdk.Tests.Concurrency;

[TestFixture]
public class DictionaryLockableAdapterTests
{
    [Test]
    public void TestConcurrentGetOrAdd()
    {
        var lockSlim = Mock.Of<ILockableCollectionSlimLock>(); 
        var adapter = new DictionaryLockableAdapter<int, int>(lockSlim);
        var computeFn = Mock.Of<Func<int, int>>();
        Mock.Get(computeFn).Setup(x => x(1)).Returns(5);

        var result = adapter.ConcurrentGetOrAdd(1, computeFn);

        result.Should().Be(5);
        adapter.ContainsKey(1).Should().BeTrue();
        adapter[1].Should().Be(5);
        
        Mock.Get(lockSlim).Verify(x => x.EnterReadLock(), Times.Once);
        Mock.Get(lockSlim).Verify(x => x.ExitReadLock(), Times.Once);
        Mock.Get(lockSlim).Verify(x => x.EnterWriteLock(), Times.Once);
        Mock.Get(lockSlim).Verify(x => x.ExitWriteLock(), Times.Once);
        
        Mock.Get(computeFn).Verify(x => x(It.IsAny<int>()), Times.Once);
        Mock.Get(computeFn).Verify(x => x(1), Times.Once);
    }
    
    [Test]
    public void TestConcurrentGetOrAddPresent()
    {
        var lockSlim = Mock.Of<ILockableCollectionSlimLock>(); 
        var adapter = new DictionaryLockableAdapter<int, int>(lockSlim);
        var computeFn = Mock.Of<Func<int, int>>();

        adapter[1] = 5;
        
        var result = adapter.ConcurrentGetOrAdd(1, computeFn);

        result.Should().Be(5);
        adapter.ContainsKey(1).Should().BeTrue();
        adapter[1].Should().Be(5);
        
        Mock.Get(lockSlim).Verify(x => x.EnterReadLock(), Times.Once);
        Mock.Get(lockSlim).Verify(x => x.ExitReadLock(), Times.Once);
        Mock.Get(lockSlim).Verify(x => x.EnterWriteLock(), Times.Never);
        Mock.Get(lockSlim).Verify(x => x.ExitWriteLock(), Times.Never);
        
        Mock.Get(computeFn).Verify(x => x(It.IsAny<int>()), Times.Never);
        Mock.Get(computeFn).Verify(x => x(1), Times.Never);
    }
    
    [Test]
    public void TestConcurrentGetOrAddPresentAfterLock()
    {
        var lockSlim = Mock.Of<ILockableCollectionSlimLock>(); 
        var adapter = new DictionaryLockableAdapter<int, int>(lockSlim);
        var computeFn = Mock.Of<Func<int, int>>();

        Mock.Get(lockSlim).Setup(x => x.EnterWriteLock()).Callback(() =>
        {
            adapter[1] = 5;
        });
        
        var result = adapter.ConcurrentGetOrAdd(1, computeFn);

        result.Should().Be(5);
        adapter.ContainsKey(1).Should().BeTrue();
        adapter[1].Should().Be(5);
        
        Mock.Get(lockSlim).Verify(x => x.EnterReadLock(), Times.Once);
        Mock.Get(lockSlim).Verify(x => x.ExitReadLock(), Times.Once);
        Mock.Get(lockSlim).Verify(x => x.EnterWriteLock(), Times.Once);
        Mock.Get(lockSlim).Verify(x => x.ExitWriteLock(), Times.Once);
        
        Mock.Get(computeFn).Verify(x => x(It.IsAny<int>()), Times.Never);
        Mock.Get(computeFn).Verify(x => x(1), Times.Never);
    }
    
    [Test]
    public void TestConcurrentGetValueOrDefault()
    {
        var lockSlim = Mock.Of<ILockableCollectionSlimLock>(); 
        var adapter = new DictionaryLockableAdapter<int, object>(lockSlim);

        var result = adapter.ConcurrentGetValueOrDefault(1);

        result.Should().BeNull();
        
        Mock.Get(lockSlim).Verify(x => x.EnterReadLock(), Times.Once);
        Mock.Get(lockSlim).Verify(x => x.ExitReadLock(), Times.Once);
        Mock.Get(lockSlim).Verify(x => x.EnterWriteLock(), Times.Never);
        Mock.Get(lockSlim).Verify(x => x.ExitWriteLock(), Times.Never);
        Mock.Get(lockSlim).Invocations.Clear();

        
        adapter[1] = 5;
        
        var result2 = adapter.ConcurrentGetValueOrDefault(1);

        result2.Should().Be(5);
        
        Mock.Get(lockSlim).Verify(x => x.EnterReadLock(), Times.Once);
        Mock.Get(lockSlim).Verify(x => x.ExitReadLock(), Times.Once);
        Mock.Get(lockSlim).Verify(x => x.EnterWriteLock(), Times.Never);
        Mock.Get(lockSlim).Verify(x => x.ExitWriteLock(), Times.Never);
    }
    
    [Test]
    public void TestConcurrentSet()
    {
        var lockSlim = Mock.Of<ILockableCollectionSlimLock>(); 
        var adapter = new DictionaryLockableAdapter<int, int>(lockSlim);

        var result = adapter.ConcurrentSet(1, 5);

        result.Should().Be(5);
        adapter.ContainsKey(1).Should().BeTrue();
        adapter[1].Should().Be(5);
        
        Mock.Get(lockSlim).Verify(x => x.EnterReadLock(), Times.Never);
        Mock.Get(lockSlim).Verify(x => x.ExitReadLock(), Times.Never);
        Mock.Get(lockSlim).Verify(x => x.EnterWriteLock(), Times.Once);
        Mock.Get(lockSlim).Verify(x => x.ExitWriteLock(), Times.Once);
    }
}