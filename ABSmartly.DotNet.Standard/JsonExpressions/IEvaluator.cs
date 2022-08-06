using System.Numerics;

namespace ABSmartly.JsonExpressions;

public interface IEvaluator
{
    object Evaluate(object expression);

    bool BooleanConvert(object x);

    // Todo: Tegu: Number is not exist in C#!!!! int, float, double, decimal, etc... It's a pain in the .. but needs to handle somehow
    int? IntConvert(object x);

    string StringConvert(object x);

    object ExtractVariable(string path);

    /// <summary>
    /// returns -1 -> lesser, 0 -> equals, 1 -> greater, null -> undefined comparison
    /// </summary>
    int? Compare(object lhs, object rhs);
}