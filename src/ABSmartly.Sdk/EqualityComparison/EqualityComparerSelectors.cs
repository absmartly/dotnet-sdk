using System.Collections;
using System.Collections.Generic;

namespace ABSmartly.EqualityComparison;

public static class EqualityComparerSelectors
{
    public static IEqualityComparer Default(object x)
    {
        return x switch
        {
            Dictionary<string, object> => new DictionaryComparer(Default),
            List<object> => new ListComparer(Default),
            _ => EqualityComparer<object>.Default
        };
    }
}