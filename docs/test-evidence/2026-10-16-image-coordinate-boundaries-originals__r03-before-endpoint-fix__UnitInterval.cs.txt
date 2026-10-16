using System.Globalization;
using System.Numerics;
namespace KuaiYi.Core;

/// <summary>Unit coordinates use the shortest round-trip decimal spelling of each double.
/// Compare that spelling exactly, so .8 + .2 touches one, while the next double above .2 does not.
/// This is not a tolerance for physical pixel coordinates or validation of arbitrary-precision JSON.</summary>
public static class UnitInterval
{
    public static bool Contains(double start,double length)
    {
        if(!double.IsFinite(start)||!double.IsFinite(length)||start<0||length<0||start>1||length>1)return false;
        var (a,sa)=Decimal(start);var (b,sb)=Decimal(length);int scale=Math.Max(sa,sb);
        return a*BigInteger.Pow(10,scale-sa)+b*BigInteger.Pow(10,scale-sb)<=BigInteger.Pow(10,scale);
    }
    private static (BigInteger Coefficient,int Scale) Decimal(double value)
    {
        // Finite [0,1] doubles have <=17 significant decimal digits and at most 340 fractional places.
        string text=value.ToString("R",CultureInfo.InvariantCulture);int exponent=0,index=text.IndexOf('E');
        if(index>=0){exponent=int.Parse(text.AsSpan(index+1),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture);text=text[..index];}
        int dot=text.IndexOf('.'),fraction=dot<0?0:text.Length-dot-1;
        return (BigInteger.Parse(text.Replace(".",""),CultureInfo.InvariantCulture),fraction-exponent);
    }
}
