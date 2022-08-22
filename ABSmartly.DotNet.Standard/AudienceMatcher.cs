using System.Collections.Generic;
using System.Text;
using ABSmartlySdk.Interfaces;
using ABSmartlySdk.JsonExpressions;

namespace ABSmartlySdk;

public class AudienceMatcher
{
    private readonly IAudienceDeserializer _audienceDeserializer;
    private readonly JsonExpr _jsonExpr;

    public AudienceMatcher(IAudienceDeserializer audienceDeserializer)
    {
        _audienceDeserializer = audienceDeserializer;
        _jsonExpr = new JsonExpr();
    }

    public bool? Evaluate(string audience, Dictionary<string, object> attributes)
    {
        if (string.IsNullOrWhiteSpace(audience))
            return null;

        var audienceBytes = Encoding.UTF8.GetBytes(audience);
        var audienceMap = _audienceDeserializer.Deserialize(audienceBytes, 0, audienceBytes.Length);

        if (audienceMap is null)
            return null;

        if (!audienceMap.TryGetValue("filter", out var filter))
            return null;

        //var type = filter.GetType();
        if (filter is not Dictionary<string, object> && filter is not List<object>) 
            return null;

        var result = _jsonExpr.EvaluateBooleanExpr(filter, attributes);
        return result;
    }
}