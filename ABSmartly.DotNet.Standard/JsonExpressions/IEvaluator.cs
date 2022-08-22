namespace ABSmartlySdk.JsonExpressions;

public interface IEvaluator
{
    object Evaluate(object expression);

    bool BooleanConvert(object p);

    // Todo: Tegu: Number is not exist in C#!!!! int, float, double, decimal, etc... It's a pain in the .. but needs to handle somehow
    int? IntConvert(object p);

    string StringConvert(object p);

    object ExtractVariable(string path);

    /// <summary>
    /// returns -1 -> lesser, 0 -> equals, 1 -> greater, null -> undefined comparison
    /// </summary>
    int? Compare(object lhs, object rhs);
}