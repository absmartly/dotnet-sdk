namespace ABSmartly.Temp;

public static class Integer
{
    //public static int RotateLeft(int value, int shift)
    //{
    //    return (value << shift) | (value >> (32 - shift));
    //}

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