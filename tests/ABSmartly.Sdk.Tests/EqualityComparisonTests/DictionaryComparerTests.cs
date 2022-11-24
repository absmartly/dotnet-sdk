using ABSmartly.Concurrency;
using ABSmartly.EqualityComparison;

namespace ABSmartly.Sdk.Tests.EqualityComparisonTests;

[TestFixture]
public class DictionaryComparerTests: ComparerTestBase
{
    private readonly DictionaryComparer _comparer;

    public DictionaryComparerTests()
    {
        _comparer = new (ValueComparerSelectorFn);
    }

    [Test]
    public void TestEquals()
    {
        var emptyDict = new Dictionary<string, object>();
        var dict1 = new Dictionary<string, object> { ["a"] = 0, ["b"] = 1 };
        var dict2 = new Dictionary<string, object> { ["a"] = 0, ["b"] = 1 };
        var dict3 = new Dictionary<string, object> { ["b"] = 1, ["a"] = 0 };
        var dict4 = new Dictionary<string, object> { ["a"] = 0, ["b"] = 1, ["c"] = 2 };
        var dict5 = new Dictionary<string, object> { ["b"] = 0, ["c"] = 1 };
        var dict6 = new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 };
        var dict7 = new Dictionary<string, object> { ["a"] = 0, ["b"] = 2 };

        _comparer.Equals(emptyDict, emptyDict).Should().Be(true);
        
        _comparer.Equals((object)dict1, (object)dict1).Should().Be(true);
        _comparer.Equals(null, (object)dict1).Should().Be(false);
        _comparer.Equals((object)dict1, null).Should().Be(false);
        
        _comparer.Equals(dict1, dict1).Should().Be(true);
        _comparer.Equals(dict1, null).Should().Be(false);
        _comparer.Equals(null, dict1).Should().Be(false);
        Mock.Get(ValueComparer).Verify(x => x.Equals(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        
        Mock.Get(ValueComparer).Invocations.Clear();
        _comparer.Equals(dict1, dict2).Should().Be(true);
        Mock.Get(ValueComparer).Verify(x => x.Equals(It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(2));
        
        Mock.Get(ValueComparer).Invocations.Clear();
        _comparer.Equals(dict1, dict3).Should().Be(true);
        Mock.Get(ValueComparer).Verify(x => x.Equals(It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(2));
        
        Mock.Get(ValueComparer).Invocations.Clear();
        _comparer.Equals(dict1, dict4).Should().Be(false);
        Mock.Get(ValueComparer).Verify(x => x.Equals(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        
        Mock.Get(ValueComparer).Invocations.Clear();
        _comparer.Equals(dict1, dict5).Should().Be(false);
        Mock.Get(ValueComparer).Verify(x => x.Equals(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        
        Mock.Get(ValueComparer).Invocations.Clear();
        _comparer.Equals(dict1, dict6).Should().Be(false);
        Mock.Get(ValueComparer).Verify(x => x.Equals(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        
        Mock.Get(ValueComparer).Invocations.Clear();
        _comparer.Equals(dict1, dict7).Should().Be(false);
        Mock.Get(ValueComparer).Verify(x => x.Equals(It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(2));
    }
    
    [Test]
    public void TestKeyComparer()
    {
        var keyComparer = Mock.Of<IComparer<string>>();
        Mock.Get(keyComparer).Setup(x => x.Compare(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((lhs, rhs) => StringComparer.Ordinal.Compare(lhs, rhs));
        var comparer = new DictionaryComparer(ValueComparerSelectorFn, keyComparer);

        var emptyDict = new Dictionary<string, object>();
        var dict1 = new Dictionary<string, object> { ["a"] = 0, ["b"] = 1 };
        var dict2 = new Dictionary<string, object> { ["a"] = 0, ["b"] = 1 };
        var dict3 = new Dictionary<string, object> { ["b"] = 1, ["a"] = 0 };
        var dict4 = new Dictionary<string, object> { ["c"] = 0, ["d"] = 1 };
        var dict5 = new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 };

        Mock.Get(keyComparer).Invocations.Clear();
        comparer.Equals(emptyDict, emptyDict).Should().Be(true);
        Mock.Get(keyComparer).Verify(x => x.Compare(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        
        Mock.Get(keyComparer).Invocations.Clear();
        comparer.Equals(dict1, dict1).Should().Be(true);
        comparer.Equals(dict1, null).Should().Be(false);
        comparer.Equals(null, dict1).Should().Be(false);
        Mock.Get(keyComparer).Verify(x => x.Compare(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        
        Mock.Get(keyComparer).Invocations.Clear();
        comparer.Equals(dict1, dict2).Should().Be(true);
        Mock.Get(keyComparer).Verify(x => x.Compare(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeast(2));
        Mock.Get(keyComparer).Verify(x => x.Compare("a", "a"), Times.Once);
        Mock.Get(keyComparer).Verify(x => x.Compare("b", "b"), Times.Once);
        
        Mock.Get(keyComparer).Invocations.Clear();
        comparer.Equals(dict1, dict3).Should().Be(true);
        Mock.Get(keyComparer).Verify(x => x.Compare(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeast(2));
        Mock.Get(keyComparer).Verify(x => x.Compare("a", "a"), Times.Once);
        Mock.Get(keyComparer).Verify(x => x.Compare("b", "b"), Times.Once);
        
        Mock.Get(keyComparer).Invocations.Clear();
        comparer.Equals(dict1, dict4).Should().Be(false);
        Mock.Get(keyComparer).Verify(x => x.Compare(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
        Mock.Get(keyComparer).Verify(x => x.Compare("a", "c"), Times.Once);
        
        Mock.Get(keyComparer).Invocations.Clear();
        comparer.Equals(dict1, dict5).Should().Be(false);
        Mock.Get(keyComparer).Verify(x => x.Compare(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
        Mock.Get(keyComparer).Verify(x => x.Compare("a", "b"), Times.AtLeastOnce);
    }

    [Test]
    public void TestConstructorMissingLock()
    {
        Func<DictionaryLockableAdapter<int, int>> act;

        act = () => new DictionaryLockableAdapter<int, int>(null);
        act.Should().Throw<ArgumentNullException>().WithMessage("Lock is required (Parameter 'lockSlim')");
        
        act = () => new DictionaryLockableAdapter<int, int>(null, new Dictionary<int, int>());
        act.Should().Throw<ArgumentNullException>().WithMessage("Lock is required (Parameter 'lockSlim')");
        
        act = () => new DictionaryLockableAdapter<int, int>(null, new Dictionary<int, int>(), EqualityComparer<int>.Default);
        act.Should().Throw<ArgumentNullException>().WithMessage("Lock is required (Parameter 'lockSlim')");
        
        act = () => new DictionaryLockableAdapter<int, int>(null, EqualityComparer<int>.Default);
        act.Should().Throw<ArgumentNullException>().WithMessage("Lock is required (Parameter 'lockSlim')");
        
        act = () => new DictionaryLockableAdapter<int, int>(null, 5);
        act.Should().Throw<ArgumentNullException>().WithMessage("Lock is required (Parameter 'lockSlim')");
    }
}