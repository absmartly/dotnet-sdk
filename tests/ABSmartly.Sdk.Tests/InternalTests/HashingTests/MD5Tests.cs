namespace ABSmartly.DotNet.Standard.UnitTests.InternalTests.HashingTests;

// Todo: decide to test the MD5 
//[TestFixture]
//public class MD5Tests
//{
//    [TestCase("", "1B2M2Y8AsgTpgAmY7PhCfg")]
//    [TestCase(" ", "chXunH2dwinSkhpA6JnsXw")]
//    [TestCase("t", "41jvpIn1gGLxDdcxa2Vkng")]
//    [TestCase("te", "Vp73JkK-D63XEdakaNaO4Q")]
//    [TestCase("tes", "KLZi2IO212_Zbk3cXpungA")]
//    [TestCase("test", "CY9rzUYh03PK3k6DJie09g")]
//    [TestCase("testy", "K5I_V6RgP8c6sYKz-TVn8g")]
//    [TestCase("testy1", "8fT8xGipOhPkZ2DncKU-1A")]
//    [TestCase("testy12", "YqRAtOz000gIu61ErEH18A")]
//    [TestCase("testy123", "pfV2H07L6WvdqlY0zHuYIw")]
//    [TestCase("special characters açb↓c", "4PIrO7lKtTxOcj2eMYlG7A")]
//    [TestCase("The quick brown fox jumps over the lazy dog", "nhB9nTcrtoJr2B01QqQZ1g")]
//    [TestCase("The quick brown fox jumps over the lazy dog and eats a pie", "iM-8ECRrLUQzixl436y96A")]
//    [TestCase("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
//        "24m7XOq4f5wPzCqzbBicLA")]
//    public void DigestBase64UrlNoPadding_Returns_Expected_Bytes(string actualString, string expectedString)
//    {
//        //var expectedBytes = Encoding.ASCII.GetBytes(expectedString);

//        //var actualBytes = Encoding.UTF8.GetBytes(actualString);
//        //var resultBytes = MD5.DigestBase64UrlNoPadding(actualBytes, 0, (uint)actualBytes.Length);
//        //Assert.That(resultBytes, Is.EqualTo(expectedBytes));
//    }

//    [TestCase("", "1B2M2Y8AsgTpgAmY7PhCfg")]
//    [TestCase(" ", "chXunH2dwinSkhpA6JnsXw")]
//    [TestCase("t", "41jvpIn1gGLxDdcxa2Vkng")]
//    [TestCase("te", "Vp73JkK-D63XEdakaNaO4Q")]
//    [TestCase("tes", "KLZi2IO212_Zbk3cXpungA")]
//    [TestCase("test", "CY9rzUYh03PK3k6DJie09g")]
//    [TestCase("testy", "K5I_V6RgP8c6sYKz-TVn8g")]
//    [TestCase("testy1", "8fT8xGipOhPkZ2DncKU-1A")]
//    [TestCase("testy12", "YqRAtOz000gIu61ErEH18A")]
//    [TestCase("testy123", "pfV2H07L6WvdqlY0zHuYIw")]
//    [TestCase("special characters açb↓c", "4PIrO7lKtTxOcj2eMYlG7A")]
//    [TestCase("The quick brown fox jumps over the lazy dog", "nhB9nTcrtoJr2B01QqQZ1g")]
//    [TestCase("The quick brown fox jumps over the lazy dog and eats a pie", "iM-8ECRrLUQzixl436y96A")]
//    [TestCase("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
//        "24m7XOq4f5wPzCqzbBicLA")]
//    public void DigestBase64UrlNoPadding_WithOffset_Returns_Expected_Bytes(string actualString, string expectedString)
//    {
//        //var expectedBytes = Encoding.ASCII.GetBytes(expectedString);

//        //var offsetBytes = Encoding.UTF8.GetBytes("123" + actualString + "321");
//        //var offsetResultBytes = MD5.DigestBase64UrlNoPadding(offsetBytes, "123".Length, (uint)(offsetBytes.Length - "123321".Length));
//        //Assert.That(expectedBytes, Is.EqualTo(offsetResultBytes));
//    }
//}