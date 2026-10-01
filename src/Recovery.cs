using System;

namespace ReiCast {
// Finite recovery attempts allow an indirect display driver to finish waking.
// Duplicate Windows notifications do not create unbounded work.
public sealed class RecoveryPlan {
    static readonly int[] Delays={0,2,6,15,30};
    DateTime started=DateTime.MinValue; int next=Delays.Length;
    public bool Pending { get { return next<Delays.Length; } }
    public string Reason="";
    public bool Schedule(DateTime now,string reason) {
        if(Pending && now-started<TimeSpan.FromSeconds(3)) return false;
        started=now;next=0;Reason=reason;return true;
    }
    public void Cancel() {next=Delays.Length;}
    public bool Take(DateTime now,out int stage) {
        stage=-1;if(!Pending || now<started.AddSeconds(Delays[next])) return false;
        // A suspended timer must not replay every missed retry in a burst.
        while(next+1<Delays.Length && now>=started.AddSeconds(Delays[next+1])) next++;
        stage=next++;return true;
    }
}
}
