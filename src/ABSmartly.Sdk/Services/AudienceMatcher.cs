using System.Collections.Generic;
using ABSmartly.JsonExpressions;

namespace ABSmartly.Services;

public class AudienceMatcher
{
    private readonly IAudienceDeserializer _audienceDeserializer;

    public AudienceMatcher(IAudienceDeserializer audienceDeserializer)
    {
        _audienceDeserializer = audienceDeserializer;
    }

    public bool? Evaluate(string audience, Dictionary<string, object> attributes)
    {
        if (string.IsNullOrWhiteSpace(audience))
            return null;

        var audienceMap = _audienceDeserializer.Deserialize(audience);

        if (audienceMap is null)
            return null;

        if (!audienceMap.TryGetValue("filter", out var filter))
            return null;

        if (filter is not Dictionary<string, object> && filter is not List<object>)
            return null;

        var result = JsonExpr.EvaluateBooleanExpr(filter, attributes);
        return result;
    }
}