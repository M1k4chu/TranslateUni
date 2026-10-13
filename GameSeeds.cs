namespace TranslateUni;
public static class GameSeeds
{
    public static int New()=>System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MaxValue);
    public static Random Stream(int seed,uint salt)
    {
        uint x=unchecked((uint)seed)^salt;
        unchecked{x^=x>>16;x*=0x7FEB352Du;x^=x>>15;x*=0x846CA68Bu;x^=x>>16;}
        return new(unchecked((int)x));
    }
}
