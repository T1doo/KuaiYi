namespace KuaiYi.Core;
/// <summary>Physical input ordering only; never authorizes OLE extraction or a translation request.</summary>
public sealed class OverlayMouseSequence
{
    private long down=-1,last=-1,owner;
    public bool OrderUnconfirmed{get;private set;}
    public bool Accept(ushort flags,bool originalHit,long time,long now,long generation=0)
    {
        // A producer handoff can remove our eventual up, even when the newer down is stale/outside the current anchor.
        if((flags&1)!=0&&down>=0&&generation>owner){OrderUnconfirmed=true;return false;}
        if(time<0||time>now||time<last)return false;
        if((flags&5)!=0)
        {
            if(!originalHit||now-time>250)return false;
            if((flags&4)!=0){last=time;down=-1;return true;}
            if(down>=0){if(owner!=generation)OrderUnconfirmed=true;return false;}
            last=time;if((flags&1)!=0){down=time;owner=generation;}return true;
        }
        if(down<0||time<down||owner!=generation)return false;
        last=time;if((flags&2)!=0)down=-1;return true;
    }
    public void Cancel(){down=-1;}
}
