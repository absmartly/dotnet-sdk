using ABSmartly.Concurrency;
using ABSmartly.EqualityComparison;

namespace ABSmartly.Sdk.Tests.EqualityComparisonTests;

[TestFixture]
public class ListComparerTests : ComparerTestBase
{
    private readonly ListComparer _comparer;

    public ListComparerTests()
    {
        _comparer = new ListComparer(ValueComparerSelectorFn);
    }

    [Test]
    public void TestEquals()
    {
        var emptyList = new List<object>();
        var list1 = new List<object> { 0, 1 };
        var list2 = new List<object> { 0, 1 };
        var list3 = new List<object> { 0, 1, 2 };
        var list4 = new List<object> { 1, 2 };
        var list5 = new List<object> { 0, 2 };

        _comparer.Equals(emptyList, emptyList).Should().Be(true);

        _comparer.Equals(list1, (object)list1).Should().Be(true);
        _comparer.Equals((object)list1, null).Should().Be(false);
        _comparer.Equals(null, (object)list1).Should().Be(false);

        _comparer.Equals(list1, list1).Should().Be(true);
        _comparer.Equals(list1, null).Should().Be(false);
        _comparer.Equals(null, list1).Should().Be(false);

        _comparer.Equals(list1, list2).Should().Be(true);
        _comparer.Equals(list1, list3).Should().Be(false);

        Mock.Get(ValueComparer).Invocations.Clear();
        _comparer.Equals(list1, list4).Should().Be(false);
        Mock.Get(ValueComparer).Verify(x => x.Equals(It.IsAny<int>(), It.IsAny<int>()), Times.Once);

        Mock.Get(ValueComparer).Invocations.Clear();
        _comparer.Equals(list1, list5).Should().Be(false);
        Mock.Get(ValueComparer).Verify(x => x.Equals(It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(2));
    }

    [Test]
    public void TestConstructorMissingLock()
    {
        Func<ListLockableAdapter<int>> act;

        act = () => new ListLockableAdapter<int>(null);
        act.Should().Throw<ArgumentNullException>().WithMessage("Lock is required (Parameter 'lockSlim')");

        act = () => new ListLockableAdapter<int>(null, new[] { 1, 2, 3 });
        act.Should().Throw<ArgumentNullException>().WithMessage("Lock is required (Parameter 'lockSlim')");
    }
}