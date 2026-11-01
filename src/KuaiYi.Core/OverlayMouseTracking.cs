namespace KuaiYi.Core;
/// <summary>Producer-side physical edge ownership; a late Raw Input up cannot retire a newer down.</summary>
public sealed class OverlayMouseTracking
{
    private readonly object sync=new();private long generation,time;private bool active;
    public long Begin(long downTime){lock(sync){time=downTime;active=true;return ++generation;}}
    public bool Read(long eventTime,bool up,out long owner,out bool ambiguous)
    {
        lock(sync)
        {
            owner=generation;ambiguous=false;if(!active||eventTime<time)return false;
            if(eventTime==time){ambiguous=up;return false;}
            if(up)active=false;return true;
        }
    }
    public void Cancel(){lock(sync){active=false;generation++;}}
    public bool Active{get{lock(sync)return active;}}
}
