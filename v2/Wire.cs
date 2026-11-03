using System;
using Fika.Core.Networking.LiteNetLib.Utils;

namespace SptDiagnostics.Possession;

public enum CommandKind : byte { Acquire, OwnerReady, ObserverReady, Failed, Heartbeat, Died }
public enum FrameKind : byte { PrepareOwner, PrepareObservers, Commit, Abort, End, Notice }

public struct HandoffCommand : INetSerializable
{
    public const int Protocol = 2;
    public int Version, Human, Bot, Entity;
    public long Token;
    public CommandKind Kind;
    public string Controller, Message;
    public ushort Operation;
    public void Serialize(NetDataWriter w)
    {
        w.Put(Version); w.Put(Human); w.Put(Bot); w.Put(Entity); w.Put(Token); w.Put((byte)Kind);
        w.Put(Controller ?? "",128); w.Put(Operation); w.Put(Message ?? "",512);
    }
    public void Deserialize(NetDataReader r)
    {
        Version=r.GetInt(); Human=r.GetInt(); Bot=r.GetInt(); Entity=r.GetInt(); Token=r.GetLong(); Kind=(CommandKind)r.GetByte();
        Controller=r.GetString(128); Operation=r.GetUShort(); Message=r.GetString(512);
        if(Kind>CommandKind.Died) throw new FormatException("Unknown handoff command");
    }
}

public struct HandoffFrame : INetSerializable
{
    public const int MaxPayload = 1024*1024;
    public int Version, Human, Bot, Entity;
    public long Token;
    public FrameKind Kind;
    public float X,Y,Z,Yaw,Pitch,Pose;
    public string Controller, HeldItem, Message;
    public ushort Operation;
    public bool KeepCorpse,Prone;
    public byte[] Payload;
    public void Serialize(NetDataWriter w)
    {
        int length=Payload?.Length ?? 0;
        if(length>MaxPayload) throw new FormatException("Handoff snapshot too large");
        w.Put(Version); w.Put(Human); w.Put(Bot); w.Put(Entity); w.Put(Token); w.Put((byte)Kind);
        w.Put(X); w.Put(Y); w.Put(Z); w.Put(Yaw); w.Put(Pitch); w.Put(Pose); w.Put(Prone);
        w.Put(Controller ?? "",128); w.Put(Operation); w.Put(HeldItem ?? "",128); w.Put(Message ?? "",512);
        w.Put(KeepCorpse); w.Put(length); if(length>0) w.Put(Payload);
    }
    public void Deserialize(NetDataReader r)
    {
        Version=r.GetInt(); Human=r.GetInt(); Bot=r.GetInt(); Entity=r.GetInt(); Token=r.GetLong(); Kind=(FrameKind)r.GetByte();
        X=r.GetFloat(); Y=r.GetFloat(); Z=r.GetFloat(); Yaw=r.GetFloat(); Pitch=r.GetFloat(); Pose=r.GetFloat(); Prone=r.GetBool();
        Controller=r.GetString(128); Operation=r.GetUShort(); HeldItem=r.GetString(128); Message=r.GetString(512); KeepCorpse=r.GetBool();
        int length=r.GetInt();
        if(length<0 || length>MaxPayload || length>r.AvailableBytes) throw new FormatException("Invalid handoff snapshot length");
        Payload=new byte[length]; if(length>0) r.GetBytes(Payload,length);
        if(Kind>FrameKind.Notice || !Finite(X) || !Finite(Y) || !Finite(Z) || !Finite(Yaw) || !Finite(Pitch)
            || !Finite(Pose) || Pose<0 || Pose>1)
            throw new FormatException("Invalid handoff frame");
    }
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
