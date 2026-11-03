using System;
using System.Linq;
using SptDiagnostics.Possession;
using Fika.Core.Networking.LiteNetLib.Utils;

static class Tests
{
    static int assertions;
    static void Check(bool ok,string name) { assertions++; if(!ok) throw new Exception(name); }
    static T RoundTrip<T>(T input) where T:struct,INetSerializable
    {
        var writer=new NetDataWriter(); input.Serialize(writer);
        var reader=new NetDataReader(writer.CopyData()); var output=new T(); output.Deserialize(reader);
        Check(reader.AvailableBytes==0,typeof(T).Name+" trailing bytes"); return output;
    }
    static void Reject(Action action,string name)
    {
        bool rejected=false; try { action(); } catch(FormatException) { rejected=true; }
        Check(rejected,name);
    }
    static void Main()
    {
        var g=new TransferGate(4,77,new[]{4,8,9});
        Check(!g.Commit(),"cannot commit before owner creates local inventory");
        Check(!g.ObserverReady(8,77),"observer cannot acknowledge before snapshot seed exists");
        Check(!g.OwnerReady(8,77),"wrong peer cannot supply inventory seed");
        Check(!g.OwnerReady(4,76),"old token cannot supply seed");
        Check(g.OwnerReady(4,77),"owner ready");
        Check(!g.OwnerReady(4,77),"duplicate seed cannot restart observer creation");
        Check(!g.ObserverReady(12,77),"outsider cannot vote");
        Check(!g.ObserverReady(8,76),"stale observer token rejected");
        Check(g.ObserverReady(8,77),"observer ready");
        Check(!g.ObserverReady(8,77),"duplicate observer cannot replace another vote");
        Check(!g.AllReady && !g.Commit(),"missing client prevents commit");
        Check(!g.RosterMatches(new[]{4,8}),"disconnect detected");
        Check(!g.RosterMatches(new[]{4,8,9,10}),"joining client changes frozen roster");
        Check(g.RosterMatches(new[]{9,4,8}),"roster order irrelevant");
        Check(g.ObserverReady(9,77) && g.AllReady && g.Commit(),"all clients permit commit");
        Check(!g.Commit() && !g.ObserverReady(9,77),"commit is one-way and idempotent");
        g.Close(); Check(g.State==TransferGate.Stage.Closed && !g.OwnerReady(4,77) && !g.Commit(),"terminal transaction cannot revive");
        var solo=new TransferGate(2,88,new[]{2});
        Check(solo.OwnerReady(2,88) && solo.AllReady && solo.Commit(),"one controlling human plus headless can commit");
        var random=new Random(771);
        for(int i=0;i<5000;i++)
        {
            int count=random.Next(1,9),owner=random.Next(count);
            var gate=new TransferGate(owner,i+100,Enumerable.Range(0,count));
            gate.OwnerReady(owner,i+100);
            foreach(var peer in Enumerable.Range(0,count).OrderBy(_=>random.Next()))
            {
                if(peer!=owner) gate.ObserverReady(peer,i+100);
                if(!gate.AllReady) Check(!gate.Commit(),"arbitrary readiness order cannot commit early");
            }
            Check(gate.Commit(),"all frozen peers eventually commit");
            gate.Close(); Check(!gate.Commit(),"late callback cannot recommit closed transaction");
        }
        var command=RoundTrip(new HandoffCommand {Version=2,Human=7,Bot=93,Entity=104,Token=123456789101,
            Kind=CommandKind.OwnerReady,Controller="012345678901234567890123",Operation=ushort.MaxValue,Message="Медицина"});
        Check(command.Version==2 && command.Human==7 && command.Bot==93 && command.Entity==104 && command.Token==123456789101
            && command.Controller=="012345678901234567890123" && command.Operation==ushort.MaxValue && command.Message=="Медицина","owner seed roundtrip");
        var payload=new byte[65537]; random.NextBytes(payload);
        var frame=RoundTrip(new HandoffFrame {Version=2,Human=7,Bot=93,Entity=104,Token=44,Kind=FrameKind.PrepareObservers,
            X=-999,Y=1,Z=999,Yaw=359,Pitch=-80,Pose=.4f,Prone=true,Controller="012345678901234567890123",Operation=65535,HeldItem="weapon",
            Message="Prepare",Payload=payload});
        Check(frame.Token==44 && frame.Entity==104 && frame.Operation==65535 && frame.X==-999 && frame.Pitch==-80
            && frame.Pose==.4f && frame.Prone && frame.HeldItem=="weapon" && frame.Payload.SequenceEqual(payload),"large binary snapshot roundtrip");
        var end=RoundTrip(new HandoffFrame {Kind=FrameKind.End,KeepCorpse=true});
        Check(end.KeepCorpse && end.Payload.Length==0,"terminal frame without snapshot");
        Reject(()=>RoundTrip(new HandoffFrame {Payload=new byte[HandoffFrame.MaxPayload+1]}),"oversized outbound state rejected");
        Reject(()=>RoundTrip(new HandoffFrame {X=float.NaN}),"nonfinite position rejected");
        Reject(()=>RoundTrip(new HandoffFrame {Pitch=float.PositiveInfinity}),"nonfinite rotation rejected");
        Reject(()=>RoundTrip(new HandoffFrame {Pose=1.1f}),"invalid stance rejected");
        Reject(()=>RoundTrip(new HandoffFrame {Kind=(FrameKind)255}),"unknown frame kind rejected");
        Reject(()=>RoundTrip(new HandoffCommand {Kind=(CommandKind)255}),"unknown command rejected");
        var w=new NetDataWriter(); new HandoffFrame().Serialize(w); var raw=w.CopyData();
        foreach(int badLength in new[]{-1,HandoffFrame.MaxPayload+1,12})
        {
            Array.Copy(BitConverter.GetBytes(badLength),0,raw,raw.Length-4,4);
            Reject(()=>new HandoffFrame().Deserialize(new NetDataReader(raw)),"invalid length rejected before allocation");
        }
        Console.WriteLine($"PASS: {assertions} assertions; handoff roster, authority, stale callbacks, commit/abort, snapshot and seed wire validation.");
    }
}
