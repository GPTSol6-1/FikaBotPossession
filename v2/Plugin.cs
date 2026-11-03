using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using EFT;
using Fika.Core.Main.Components;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Modding;
using Fika.Core.Modding.Events;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using HarmonyLib;
using UnityEngine;

namespace SptDiagnostics.Possession;

[BepInPlugin(Id,"GPTSol6-1-FikaBotPossession","1.0.1")]
[BepInDependency("com.fika.core","2.4.6")]
[BepInDependency("com.lir.liveraid",BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("me.sol.sain",BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.mpstark.dynamicmaps",BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Id="com.sptdiagnostics.fikabotpossession";
    internal static Plugin Instance;
    internal IFikaNetworkManager Network;
    internal Host Host;
    internal Client Client;
    internal ConfigEntry<bool> Allow;
    internal ConfigEntry<KeyCode> TakeKey;
    internal ConfigEntry<KeyboardShortcut> EndKey;
    internal CoopHandler Coop => Network?.CoopHandler;
    internal bool IsHost => Network is FikaServer;
    internal bool Headless => FikaBackendUtils.IsHeadless;
    Harmony harmony;
    sealed class Identity { internal string Profile; internal int? NetId; internal bool Dead; }
    readonly Dictionary<NetPeer,Identity> identities=new();
    internal new void Info(string text) => Logger.LogInfo(text);
    internal void Error(Exception error) => Logger.LogError(error);
    void Awake()
    {
        Instance=this;
        if(typeof(FikaPlayer).Assembly.GetName().Version!=new System.Version(2,4,6,0))
        { Info("1.0.1 requires Fika.Core 2.4.6.0; no hooks installed."); return; }
        Allow=Config.Bind("Host","Enable possession",true,"Allow experimental local-player handoff on a dedicated headless host.");
        TakeKey=Config.Bind("Controls","Take over",KeyCode.F6,"Request the bot in the center of the spectator camera.");
        EndKey=Config.Bind("Controls","End possession",new KeyboardShortcut(KeyCode.F7),"Kill the possessed character and return to freecam, leaving its corpse and items for looting. Does not change your original raid result.");
        Client=new Client(this);
        harmony=new Harmony(Id);
        try { RuntimeContract.Validate(); Hooks.Install(harmony); }
        catch(Exception e) { harmony.UnpatchSelf(); Client=null; Error(e); return; }
        FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerCreatedEvent>(Created);
        FikaEventDispatcher.SubscribeEvent<FikaNetworkManagerDestroyedEvent>(Destroyed);
        FikaEventDispatcher.SubscribeEvent<PeerDisconnectedEvent>(Disconnected);
        Info("1.0.1 loaded. One-way takeover; dedicated host and Fika.Core 2.4.6.0 required.");
    }
    void Created(FikaNetworkManagerCreatedEvent e)
    {
        Reset(); Network=e.Manager;
        if(IsHost)
        {
            Host=new Host(this);
            Network.RegisterPacket<HandoffCommand,NetPeer>((q,peer)=>Guard(()=>Host?.Receive(q,peer)));
        }
        else Network.RegisterPacket<HandoffCommand>(_=>{});
        Network.RegisterPacket<HandoffFrame>(q=> { if(!IsHost) Client?.Receive(q); });
    }
    void Destroyed(FikaNetworkManagerDestroyedEvent e) { if(e.Manager==Network) Reset(); }
    void Disconnected(PeerDisconnectedEvent e)
    {
        if(e.NetworkManager!=Network) return;
        Guard(()=>Host?.Disconnect(e.Peer)); identities.Remove(e.Peer);
    }
    void Update() => Guard(()=> { Cache(); Host?.Tick(); if(!Headless) Client?.Tick(); });
    void OnGUI() { if(!Headless) Client?.Draw(); }
    void OnApplicationFocus(bool focus) { if(!Headless) Client?.Focus(focus); }
    void OnDestroy()
    {
        Reset(); harmony?.UnpatchSelf();
        FikaEventDispatcher.UnsubscribeEvent<FikaNetworkManagerCreatedEvent>(Created);
        FikaEventDispatcher.UnsubscribeEvent<FikaNetworkManagerDestroyedEvent>(Destroyed);
        FikaEventDispatcher.UnsubscribeEvent<PeerDisconnectedEvent>(Disconnected);
        Instance=null;
    }
    void Reset()
    {
        Host?.Dispose(); Host=null; Client?.Reset(); identities.Clear(); Network=null;
    }
    internal void Guard(Action action)
    {
        try { action(); } catch(Exception e) { Error(e); }
    }
    internal void Bind(NetPeer peer,string profile)
    {
        if(IsHost && peer!=null && !string.IsNullOrEmpty(profile) && !identities.ContainsKey(peer))
            identities.Add(peer,new Identity {Profile=profile});
    }
    void Cache()
    {
        if(!IsHost || Coop==null) return;
        foreach(var i in identities.Values)
        {
            var h=Coop.HumanPlayers.FirstOrDefault(x=>x!=null && x.ProfileId==i.Profile);
            if(h!=null) { i.NetId=h.NetId; i.Dead=h.HealthController?.IsAlive==false; }
        }
    }
    internal void Remember(FikaPlayer human,bool dead)
    {
        if(human==null || human.IsAI) return;
        foreach(var i in identities.Values.Where(x=>x.Profile==human.ProfileId)) { i.NetId=human.NetId; i.Dead=dead; }
    }
    internal bool Eligible(NetPeer peer,int human)
    {
        Cache();
        return Coop?.LocalGameInstance!=null && peer!=null && identities.TryGetValue(peer,out var i) && i.NetId==human
            && (i.Dead || Coop.LocalGameInstance.ExtractedPlayers.Contains(human));
    }
    internal bool Eligible(FikaPlayer human) => human!=null && !human.IsAI && Coop?.LocalGameInstance!=null
        && (!human.HealthController.IsAlive || Coop.LocalGameInstance.ExtractedPlayers.Contains(human.NetId));
    internal void Send(HandoffCommand q)
    {
        q.Version=HandoffCommand.Protocol;
        Network?.SendData(ref q,q.Kind==CommandKind.Heartbeat ? DeliveryMethod.Unreliable : DeliveryMethod.ReliableOrdered);
    }
    internal void Frame(NetPeer peer,HandoffFrame q)
    {
        q.Version=HandoffCommand.Protocol;
        if(peer==null) Network.SendData(ref q,DeliveryMethod.ReliableOrdered);
        else Network.SendDataToPeer(ref q,DeliveryMethod.ReliableOrdered,peer);
    }
}
