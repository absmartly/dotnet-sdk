using System.Collections.Generic;
using System.Text;
using ABSmartly.DotNet.Nio.Charset;
using ABSmartly.JsonExpressions;
using ABSmartly.Temp;

namespace ABSmartly;

public class AudienceMatcher
{
    private readonly IAudienceDeserializer _audienceDeserializer;
    private readonly JsonExpr _jsonExpr;

    public AudienceMatcher(IAudienceDeserializer audienceDeserializer)
    {
        _audienceDeserializer = audienceDeserializer;
        _jsonExpr = new JsonExpr();
    }

    public Result Evaluate(string audience, Dictionary<string, object> attributes)
    {
        var bytes = Encoding.UTF8.GetBytes(audience);
        var audienceMap = _audienceDeserializer.Deserialize(bytes, 0, bytes.Length);

        if (audienceMap is null)
            return null;

        if (!audienceMap.TryGetValue("filter", out var filter))
            return null;

        //object filter = audienceMap.Get("filter");
        // Todo: Tegu: Generic type won't work out of the box, Dictionary and List of what type to look for?
        if (filter is Dictionary<string, object> || filter is List<string>)
        {
            return new Result(_jsonExpr.EvaluateBooleanExpr(filter, attributes));
        }

        return null;
    }
}