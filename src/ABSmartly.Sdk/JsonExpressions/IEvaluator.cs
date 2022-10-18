namespace ABSmartly.JsonExpressions;

public interface IEvaluator
{
    object Evaluate(object expression);

    bool? BooleanConvert(object p);

    double? NumberConvert(object p);

    string StringConvert(object p);

    object ExtractVariable(string path);

    /// <summary>
    /// returns -1 -> lesser, 0 -> equals, 1 -> greater, null -> undefined comparison
    /// </summary>
    int? Compare(object lhs, object rhs);
}