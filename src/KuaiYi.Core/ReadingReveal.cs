using System.Globalization;
namespace KuaiYi.Core;

/// <summary>Presentation-only pacing; owns no source strings or request state.</summary>
public sealed class ReadingReveal
{
    private Guid? version;private int shown;private double last,budget;
    public void Reset(){version=null;shown=0;last=budget=0;}
    public int Advance(Guid? selected,string text,double seconds,bool immediate)
    {
        if(!double.IsFinite(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
        if(version!=selected||shown>text.Length){version=selected;shown=0;budget=0;last=seconds;}
        if(immediate||selected==null){shown=text.Length;budget=0;last=seconds;return shown;}
        double elapsed=Math.Clamp(seconds-last,0,.1);last=seconds;
        int remaining=text.Length-shown;
        budget+=elapsed*Math.Max(42,remaining/0.18);
        int end=Math.Min(text.Length,shown+(int)budget);
        if(shown==0&&text.Length>0)end=Math.Max(end,1);
        int next=shown;
        while(next<end)next+=StringInfo.GetNextTextElementLength(text.AsSpan(next));
        budget=Math.Max(0,budget-(next-shown));shown=next;
        if(shown==text.Length)budget=0;
        return shown;
    }
}
