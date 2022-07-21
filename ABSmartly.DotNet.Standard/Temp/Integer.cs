namespace ABSmartly.Temp;

public static class Integer
{
    //public static int RotateLeft(int value, int shift)
    //{
    //    return (value << shift) | (value >> (32 - shift));
    //}

    //
    // // Todo: https://www.geeksforgeeks.org/integer-rotateleft-method-in-java/

    public static int RotateLeft(int value, int shift)
    {
        unchecked
        {
            uint uvalue = (uint) value;
            uint uresult = (uvalue << shift) | (uvalue >> 32 - shift);
            return (int) uresult;
        }
    }
}