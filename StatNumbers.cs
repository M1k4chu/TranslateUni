using System.Globalization;
namespace TranslateUni;
public static class StatNumbers
{
    public static string Format(double value,bool currency,bool percentage,bool change,int width=30)
    {
        value=Math.Round(value,currency||percentage?2:0,MidpointRounding.AwayFromZero);
        if(value==0)value=0; // Remove negative zero before formatting and choosing a sign.
        string sign=value<0?"-":change&&value>0?"+":"",prefix=sign+(currency?"$":""),suffix=percentage?(change?"pp":"%" ):"";
        double absolute=Math.Abs(value);
        string number=absolute.ToString(currency&&change?"0.00":currency||percentage?"0.##":"0",CultureInfo.InvariantCulture);
        string full=prefix+number+suffix;
        if(full.Length<=width)return full;
        foreach(var scale in new[]{(Factor:1e12,Unit:"t"),(Factor:1e9,Unit:"b"),(Factor:1e6,Unit:"m"),(Factor:1e3,Unit:"k")})
            if(absolute>=scale.Factor)
                foreach(string format in new[]{"0.##","0.#","0"})
                {string compact=prefix+(absolute/scale.Factor).ToString(format,CultureInfo.InvariantCulture)+scale.Unit+suffix;if(compact.Length<=width)return compact;}
        foreach(string format in new[]{"0.#","0"})
        {string rounded=prefix+absolute.ToString(format,CultureInfo.InvariantCulture)+suffix;if(rounded.Length<=width)return rounded;}
        return full;
    }
}
