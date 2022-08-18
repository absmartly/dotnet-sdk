using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace ABSmartly.Utils.NewtonsoftJsonUtils;

public class JsonUtils
{
    public static Dictionary<string, object> ParseJsonDictionaryOfStringObject(Dictionary<string, object> dictionary)
    {
        var parsedObject = ParseJsonObject(dictionary);
        return parsedObject as Dictionary<string, object>;
    }


    public static object ParseJsonObject(object json)
    {
        if (json is Dictionary<string, object> jsonDict)
            return ParseDictionaryOfStringObject(jsonDict);

        if (json is JArray jArray)
            return ParseJArray(jArray);

        if (json is JObject jObject)
            return ParseJObject(jObject);

        if (json is JValue jValue)
            return jValue.Value;

        return json;
    }

    private static object ParseDictionaryOfStringObject(IDictionary<string, object> jsonDict)
    {
        for (var i = 0; i < jsonDict.Count; i++)
        {
            var kvpKey = jsonDict.ElementAt(i).Key;

            var kvpValue = ParseJsonObject(jsonDict.ElementAt(i).Value);
            jsonDict[kvpKey] = kvpValue;
        }

        return jsonDict;
    }

    private static object ParseJArray(JArray jArray)
    {
        var list = new List<object>();

        foreach (var jArrayItem in jArray)
        {
            var parsedJArrayItem = ParseJsonObject(jArrayItem);
            list.Add(parsedJArrayItem);
        }

        return list;
    }

    private static object ParseJObject(JObject jObject)
    {
        var dict = new Dictionary<string, object>();

        foreach (var jObjectKvP in jObject)
        {
            dict.Add(jObjectKvP.Key, ParseJsonObject(jObjectKvP.Value?.ToObject<object>()));
        }

        return dict;
    }
}