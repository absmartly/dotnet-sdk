namespace ABSmartly.DotNet.Util;

public class Objects
{
    public new static bool Equals(object a, object b) {
        return (a == b) || (a != null && a.Equals(b));
    }

    // Todo: review, needed?

    //public static int hash(Object... values) {
    //    return Arrays.hashCode(values);
    //}

    public static int Hash()
    {
        return 0;
    }

    public static int Hash(object o1)
    {
        return 0;
    }

    public static int Hash(object o1, object o2)
    {
        return 0;
    }

    public static int Hash(object[] os)
    {
        return 0;
    }

}