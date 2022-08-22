using ABSmartlySdk;
using ABSmartlySdk.DefaultServiceImplementations;
using ABSmartlySdk.Interfaces;
using Moq;

namespace ABSmartly.DotNet.Standard.UnitTests;

[TestFixture]
public class AudienceMatcherTests
{
    [TestCase("")]
    [TestCase(null)]
    public void Evaluate_NullOrEmptyAudienceString_Returns_Null(string audience)
    {
        var audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer(null));

        var result = audienceMatcher.Evaluate(audience, null);
        Assert.That(result, Is.Null);
    }

    [TestCase("{}")]
    [TestCase("{\"not-filter\":[{\"value\":5}]}")]
    public void Evaluate_EmptyOrNotContainsFilterAudienceArray_Returns_Null(string audience)
    {
        var audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer(null));

        var result = audienceMatcher.Evaluate(audience, null);
        Assert.That(result, Is.Null);
    }

    [TestCase("{\"filter\":[{\"value\":5}]}")]
    public void Evaluate_DeserializeResultNull_Returns_Null(string audience)
    {
        var audienceDeserializer = new Mock<IAudienceDeserializer>();
        audienceDeserializer.Setup(q => q.Deserialize(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((Dictionary<string, object>)null);

        var audienceMatcher = new AudienceMatcher(audienceDeserializer.Object);

        var result = audienceMatcher.Evaluate(audience, null);
        Assert.That(result, Is.Null);
    }

    [TestCase("{\"filter\":null}")]
    [TestCase("{\"filter\":false}")]
    [TestCase("{\"filter\":5}")]
    [TestCase("{\"filter\":\"a\"}")]
    public void Evaluate_Audience_IsNot_DictionaryOfStringObject_ListOfObject_Returns_Null(string audience)
    {
        var audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer(null));

        var result = audienceMatcher.Evaluate(audience, null);
        Assert.That(result, Is.Null);
    }

    [TestCase("{\"filter\":[{\"value\":5}]}", true)]
    [TestCase("{\"filter\":[{\"value\":true}]}", true)]
    [TestCase("{\"filter\":[{\"value\":1}]}", true)]
    [TestCase("{\"filter\":[{\"value\":null}]}", false)]
    [TestCase("{\"filter\":[{\"value\":0}]}", false)]
    public void Evaluate_CorrectValue_Returns_ExpectedResult(string audience, bool expectedResult)
    {
        var audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer(null));

        var result = audienceMatcher.Evaluate(audience, null);
        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [TestCase("{\"filter\":[{\"not\":{\"var\":\"returning\"}}]}", true, false)]
    [TestCase("{\"filter\":[{\"not\":{\"var\":\"returning\"}}]}", false, true)]
    public void Evaluate_NotOperator_WithAttribute_Returns_ExpectedResult(string audience, bool value, bool expectedResult)
    {
        var audienceMatcher = new AudienceMatcher(new DefaultAudienceDeserializer(null));

        var attributes = new Dictionary<string, object>()
        {
            { "returning", value }
        };
        var result = audienceMatcher.Evaluate(audience, attributes);
        Assert.That(result, Is.EqualTo(expectedResult));
    }




}