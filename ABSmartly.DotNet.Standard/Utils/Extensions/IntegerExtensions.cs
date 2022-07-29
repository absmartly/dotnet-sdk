namespace ABSmartly.Utils.Extensions;

// Todo: check that this extension is not exposed outside of the project! A lazy, but not a good practice to add extension methods for default types
internal static class IntegerExtensions
{
    internal static int GetNumberOfLeadingZeros(this int x)
    {
        const int numIntBits = sizeof(int) * 8; //compile time constant
        //do the smearing
        x |= x >> 1; 
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;
        //count the ones
        x -= x >> 1 & 0x55555555;
        x = (x >> 2 & 0x33333333) + (x & 0x33333333);
        x = (x >> 4) + x & 0x0f0f0f0f;
        x += x >> 8;
        x += x >> 16;
        return numIntBits - (x & 0x0000003f); //subtract # of 1s from 32
    }
}