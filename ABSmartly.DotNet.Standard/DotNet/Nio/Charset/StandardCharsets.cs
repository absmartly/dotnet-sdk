using System.Text;

namespace ABSmartly.DotNet.Nio.Charset;

public class StandardCharsets
{
    //Todo: If ASCII is not good, than look for / implement a specific converter to: 'us_ascii'
    public static Encoding US_ASCII => Encoding.ASCII;
    public static Encoding UTF_8 => Encoding.UTF8;
}