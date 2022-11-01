﻿using Moq;
// ReSharper disable ExpressionIsAlwaysNull

namespace ABSmartly.DotNet.Standard.UnitTests.JsonExpressionsTests.OperatorsTests;

[TestFixture]
public class InOperatorTests : TestCases
{
#pragma warning disable CS8618
    private Mock<IEvaluator> evaluator;
    private InOperator inOperator;
#pragma warning restore CS8618

    [SetUp]
    public void Init()
    {
        evaluator = new Mock<IEvaluator>();

        evaluator.Setup(e => e.StringConvert(It.IsAny<object>()))
            .Returns(StringConvert);

        evaluator.Setup(e => e.Compare(It.IsAny<object>(), It.IsAny<object>()))
            .Returns(CompareAsString);

        inOperator = new InOperator();
    }

    #region Non Valid haystack

    [TestCaseSource(nameof(NonStringOrIListOrIDictionary))]
    public void Binary_NonStringOrIListOrIDictionary_Returns_Null(object haystack)
    {
        var needle = "unused";
        var result = inOperator.Binary(evaluator.Object, haystack, needle);

        Assert.That(result, Is.Null);
    }

    [TestCaseSource(nameof(NonStringOrIListOrIDictionary))]
    public void Binary_NonStringOrIListOrIDictionary_Verify_StringConvertCalledZeroTimes(object haystack)
    {
        var needle = "unused";
        inOperator.Binary(evaluator.Object, haystack, needle);

        evaluator.Verify(ev => ev.StringConvert(It.IsAny<object>()), Times.Exactly(0));
    }

    [TestCaseSource(nameof(NonStringOrIListOrIDictionary))]
    public void Binary_NonStringOrIListOrIDictionary_Verify_CompareCalledZeroTimes(object haystack)
    {
        var needle = "unused";
        inOperator.Binary(evaluator.Object, haystack, needle);

        evaluator.Verify(ev => ev.Compare(It.IsAny<object>(), It.IsAny<object>()), Times.Exactly(0));
    }    

    #endregion

    #region String - NullNeedle

    [TestCaseSource(nameof(StringsWithEmpty))]
    public void Binary_StringWithNullNeedle_Returns_False(object haystack)
    {
        string needle = null;
        var result = inOperator.Binary(evaluator.Object, haystack, needle);

        Assert.That((bool)result, Is.False);
    }

    [TestCaseSource(nameof(StringsWithEmpty))]
    public void Binary_StringWithNullNeedle_Verify_StringConvertCalledZeroTimes(object haystack)
    {
        string needle = null;
        inOperator.Binary(evaluator.Object, haystack, needle);

        evaluator.Verify(ev => ev.StringConvert(It.IsAny<object>()), Times.Exactly(0));
    }

    [TestCaseSource(nameof(StringsWithEmpty))]
    public void Binary_StringWithNullNeedle_Verify_CompareCalledZeroTimes(object haystack)
    {
        string needle = null;
        inOperator.Binary(evaluator.Object, haystack, needle);

        evaluator.Verify(ev => ev.Compare(It.IsAny<object>(), It.IsAny<object>()), Times.Exactly(0));
    }    

    #endregion
    #region String - EmptyNeedle

    [TestCaseSource(nameof(StringsWithEmpty))]
    public void Binary_StringWithEmptyNeedle_Returns_False(object haystack)
    {
        var needle = "";
        var result = inOperator.Binary(evaluator.Object, haystack, needle);

        Assert.That((bool)result, Is.False);
    }    

    [TestCaseSource(nameof(StringsWithEmpty))]
    public void Binary_StringWithEmptyNeedle_Verify_StringConvertCalledOneTime(object haystack)
    {
        string needle = "";
        inOperator.Binary(evaluator.Object, haystack, needle);

        evaluator.Verify(ev => ev.StringConvert(It.IsAny<object>()), Times.Exactly(1));
    }

    [TestCaseSource(nameof(StringsWithEmpty))]
    public void Binary_StringWithEmptyNeedle_Verify_CompareCalledZeroTime(object haystack)
    {
        string needle = "";
        inOperator.Binary(evaluator.Object, haystack, needle);

        evaluator.Verify(ev => ev.Compare(It.IsAny<object>(), It.IsAny<object>()), Times.Exactly(0));
    }


    #endregion
    #region String - Ok

    [TestCaseSource(nameof(String_Object))]
    public void Binary_String_Returns_HayStackContainsStringifiedNeedle(object haystack, object needle)
    {
        var result = inOperator.Binary(evaluator.Object, haystack, needle);

        var expectedResult = haystack.ToString()!.Contains(needle.ToString()!);

        Assert.That(result, Is.EqualTo(expectedResult));
    }    

    [TestCaseSource(nameof(String_Object))]
    public void Binary_String_Verify_StringConvertCalledOneTime(object haystack, object needle)
    {
        inOperator.Binary(evaluator.Object, haystack, needle);

        evaluator.Verify(ev => ev.StringConvert(It.IsAny<object>()), Times.Exactly(1));
    }

    [TestCaseSource(nameof(String_Object))]
    public void Binary_String_Verify_CompareCalledZeroTime(object haystack, object needle)
    {
        inOperator.Binary(evaluator.Object, haystack, needle);

        evaluator.Verify(ev => ev.Compare(It.IsAny<object>(), It.IsAny<object>()), Times.Exactly(0));
    }

    #endregion

    #region List - Empty

    [TestCaseSource(nameof(ListOfObjectEmpty))]
    public void Binary_ListEmpty_Returns_False(object haystack)
    {
        var needle = "unused";
        var result = inOperator.Binary(evaluator.Object, haystack, needle);

        Assert.That((bool)result, Is.False);
    }    

    [TestCaseSource(nameof(ListOfObjectEmpty))]
    public void Binary_ListEmpty_Verify_StringConvertCalledZeroTimes(object haystack)
    {
        var needle = "unused";
        inOperator.Binary(evaluator.Object, haystack, needle);

        evaluator.Verify(ev => ev.StringConvert(It.IsAny<object>()), Times.Exactly(0));
    }

    [TestCaseSource(nameof(ListOfObjectEmpty))]
    public void Binary_ListEmpty_Verify_CompareCalledZeroTimes(object haystack)
    {
        var needle = "unused";
        inOperator.Binary(evaluator.Object, haystack, needle);

        evaluator.Verify(ev => ev.Compare(It.IsAny<object>(), It.IsAny<object>()), Times.Exactly(0));
    }    

    #endregion
    #region List - Ok

    [TestCaseSource(nameof(ListOfObject_Object))]
    public void Binary_List_Returns_ListItem_Needle_CompareResult(object haystack, object needle)
    {
        var result = inOperator.Binary(evaluator.Object, haystack, needle);

        var expectedResult = false;
        foreach (var item in (List<object>)haystack)
        {
           if (CompareAsString(item, needle) == 0)
               expectedResult = true;
        }

        Assert.That(result, Is.EqualTo(expectedResult));
    }    

    [TestCaseSource(nameof(ListOfObject_Object))]
    public void Binary_List_Verify_StringConvertCalledZeroTimes(object haystack, object needle)
    {
        inOperator.Binary(evaluator.Object, haystack, needle);

        evaluator.Verify(ev => ev.StringConvert(It.IsAny<object>()), Times.Exactly(0));
    }

    [TestCaseSource(nameof(ListOfObject_Object))]
    public void Binary_List_Verify_CompareCalledNumberOfNonMatchesInSequence(object haystack, object needle)
    {
        inOperator.Binary(evaluator.Object, haystack, needle);

        var expectedResult = 0;
        foreach (var item in (List<object>)haystack)
        {
            expectedResult++;
            if (CompareAsString(item, needle) == 0)
                break;
        }

        evaluator.Verify(ev => ev.Compare(It.IsAny<object>(), It.IsAny<object>()), Times.Exactly(expectedResult));
    }   

    #endregion

    #region Dictionary - NullNeedle

    [TestCaseSource(nameof(DictionaryStringKeysWithNullValues))]
    public void Binary_DictionaryWithNullNeedle_Returns_False(object haystack)
    {
        string needle = null;
        var result = inOperator.Binary(evaluator.Object, haystack, needle);

        Assert.That((bool)result, Is.False);
    }    

    [TestCaseSource(nameof(DictionaryStringKeysWithNullValues))]
    public void Binary_DictionaryWithNullNeedle_Verify_StringConvertCalledZeroTimes(object haystack)
    {
        string needle = null;
        inOperator.Binary(evaluator.Object, haystack, needle);

        evaluator.Verify(ev => ev.StringConvert(It.IsAny<object>()), Times.Exactly(0));
    }

    [TestCaseSource(nameof(DictionaryStringKeysWithNullValues))]
    public void Binary_DictionaryWithNullNeedle_Verify_CompareCalledZeroTimes(object haystack)
    {
        string needle = null;
        inOperator.Binary(evaluator.Object, haystack, needle);
        
        evaluator.Verify(ev => ev.Compare(It.IsAny<object>(), It.IsAny<object>()), Times.Exactly(0));
    }    

    #endregion
    #region Dictionary - EmptyNeedle

    [TestCaseSource(nameof(DictionaryStringKeysWithNullValues))]
    public void Binary_DictionaryWithEmptyNeedle_Returns_False(object haystack)
    {
        string needle = "";
        var result = inOperator.Binary(evaluator.Object, haystack, needle);

        Assert.That((bool)result, Is.False);
    }

    [TestCaseSource(nameof(DictionaryStringKeysWithNullValues))]
    public void Binary_DictionaryWithEmptyNeedle_Verify_StringConvertCalledOneTimes(object haystack)
    {
        string needle = "";
        inOperator.Binary(evaluator.Object, haystack, needle);

        evaluator.Verify(ev => ev.StringConvert(It.IsAny<object>()), Times.Exactly(1));
    }

    [TestCaseSource(nameof(DictionaryStringKeysWithNullValues))]
    public void Binary_DictionaryWithEmptyNeedle_Verify_CompareCalledZeroTimes(object haystack)
    {
        string needle = "";
        inOperator.Binary(evaluator.Object, haystack, needle);
        
        evaluator.Verify(ev => ev.Compare(It.IsAny<object>(), It.IsAny<object>()), Times.Exactly(0));
    }

    #endregion
    #region Dictionary - Ok

    [TestCaseSource(nameof(DictionaryStringWithNullValues_Object))]
    public void Binary_Dictionary_Returns_ContainsKeyOfStringifiedNeedle(object haystack, object needle)
    {
        var result = inOperator.Binary(evaluator.Object, haystack, needle);

        var dict = (IDictionary<string, object>)haystack;
        var expectedResult = dict.ContainsKey(needle.ToString()!);

        Assert.That(result, Is.EqualTo(expectedResult));
    }    

    [TestCaseSource(nameof(DictionaryStringWithNullValues_Object))]
    public void Binary_Dictionary_Verify_StringConvertCalledOneTimes(object haystack, object needle)
    {
        inOperator.Binary(evaluator.Object, haystack, needle);

        evaluator.Verify(ev => ev.StringConvert(It.IsAny<object>()), Times.Exactly(1));
    }

    [TestCaseSource(nameof(DictionaryStringWithNullValues_Object))]
    public void Binary_Dictionary_Verify_CompareCalledZeroTimes(object haystack, object needle)
    {
        inOperator.Binary(evaluator.Object, haystack, needle);
        
        evaluator.Verify(ev => ev.Compare(It.IsAny<object>(), It.IsAny<object>()), Times.Exactly(0));
    }

    #endregion


    #region Helper

    private static string StringConvert(object ob)
    {
        return ob.ToString();
    }

    private static int? CompareAsString(object lhs, object rhs)
    {
        if (lhs.ToString() == rhs.ToString())
            return 0;

        //if (lhs.ToString() == "null")
        //    return null;

        //if (lhs.ToString() == "-1")
        //    return -1;

        //if (lhs.ToString() == "0")
        //    return 0;

        //if (lhs.ToString() == "1")
        //    return 1;

        return null;
    }

    #endregion
}