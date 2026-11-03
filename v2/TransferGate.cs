using System.Collections.Generic;
using System.Linq;

namespace SptDiagnostics.Possession;

// Transport-independent transaction gate. Only the selected owner can supply the
// inventory seed; control starts only when every peer in the frozen roster is ready.
public sealed class TransferGate
{
    public enum Stage { Owner, Observers, Active, Closed }
    public Stage State { get; private set; }
    public int Owner { get; }
    public long Token { get; }
    readonly HashSet<int> roster;
    readonly HashSet<int> ready=new();
    public TransferGate(int owner,long token,IEnumerable<int> peers)
    {
        Owner=owner; Token=token; roster=new(peers);
        if(!roster.Contains(owner)) throw new System.ArgumentException("Owner must be a participant");
    }
    public bool OwnerReady(int peer,long token)
    {
        if(State!=Stage.Owner || peer!=Owner || token!=Token) return false;
        State=Stage.Observers; ready.Add(peer); return true;
    }
    public bool ObserverReady(int peer,long token)
    {
        if(State!=Stage.Observers || token!=Token || !roster.Contains(peer)) return false;
        return ready.Add(peer);
    }
    public bool AllReady => State==Stage.Observers && roster.SetEquals(ready);
    public bool Commit()
    {
        if(!AllReady) return false;
        State=Stage.Active; return true;
    }
    public bool RosterMatches(IEnumerable<int> peers) => roster.SetEquals(peers);
    public bool Participant(int peer) => roster.Contains(peer);
    public void Close() => State=Stage.Closed;
}
