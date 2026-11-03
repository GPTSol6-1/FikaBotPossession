using System;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.PacketHandlers;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib.Utils;
using HarmonyLib;
using Systems.Effects;
using UnityEngine;

namespace SptDiagnostics.Possession;

internal static class Entities
{
    internal static GameWorld World => Singleton<GameWorld>.Instance;
    internal static byte[] Snapshot(FikaPlayer bot)
    {
        // Roundtrip before altering the identity: no inventory/profile objects are
        // shared with the suspended AI or with the original human's profile.
        var w=new NetDataWriter(); w.PutProfile(bot.Profile);
        var profile=new NetDataReader(w.CopyData()).GetProfile();
        profile.Id=MongoID.Generate(true).ToString();
        profile.Health=bot.ActiveHealthController.Store();
        profile.QuestsData?.Clear(); profile.AchievementsData?.Clear(); profile.TaskConditionCounters?.Clear();
        w.Reset(); w.PutProfile(profile);
        var health=bot.ActiveHealthController.SerializeState();
        w.Put(health.Length); w.Put(health);
        var result=w.CopyData();
        if(result.Length>HandoffFrame.MaxPayload) throw new InvalidOperationException("Bot state exceeds snapshot limit");
        return result;
    }
    internal static (Profile Profile,byte[] Health) Decode(byte[] bytes)
    {
        var r=new NetDataReader(bytes); var profile=r.GetProfile(); int count=r.GetInt();
        if(count<1 || count>r.AvailableBytes) throw new FormatException("Invalid health snapshot");
        var health=new byte[count]; r.GetBytes(health,count);
        if(r.AvailableBytes!=0) throw new FormatException("Trailing state data");
        return (profile,health);
    }
    internal static Vector3 Position(HandoffFrame q) => new(q.X,q.Y,q.Z);
    internal static async Task<ObservedPlayer> Observe(Plugin p,HandoffFrame q)
    {
        var network=p.Network;
        var world=World;
        var data=Decode(q.Payload);
        ObservedPlayer entity;
        try
        {
            entity=await ObservedPlayer.CreateObservedPlayer(world,q.Entity,Position(q),Quaternion.Euler(0,q.Yaw,0),
            "Player","Possessed_"+q.Entity+"_",EPointOfView.ThirdPerson,data.Profile,data.Health,false,EUpdateQueue.Update,
            Player.EUpdateMode.Manual,Player.EUpdateMode.Auto,AppEnvironment.Config.CharacterController.ObservedPlayerMode,
            ()=>1f,()=>1f,ObservedViewFilter.Default,
            new MongoID(q.Controller),q.Operation,false);
        }
        catch { CleanupIncomplete(p,"Possessed_"+q.Entity+"_"); throw; }
        if(p.Network!=network || World!=world || world==null)
        {
            Remove(p,entity);
            throw new OperationCanceledException("Raid changed while preparing observed character");
        }
        try
        {
        // Do not call InitObservedPlayer: its human-join cosmetics change faction
        // and discard the dogtag. This character keeps the selected bot's faction.
        entity.PacketSender=entity.gameObject.AddComponent<ObservedPacketSender>();
        // Observed characters replay vault state; they must not also run the
        // local vault simulation created by EFT's general player initializer.
        if(entity.VaultingComponent!=null) entity.UpdateEvent-=entity.VaultingComponent.DoVaultingTick;
        foreach(var name in new[]{"_vaultingComponent","_vaultingComponentDebug","_vaultingParameters","_vaultingGameplayRestrictions"})
            AccessTools.Field(typeof(Player),name).SetValue(entity,null);
        var observedVault=AccessTools.Field(typeof(ObservedPlayer),"_observedVaultingParameters").GetValue(entity);
        AccessTools.Method(typeof(Player),"InitVaultingAudioControllers").Invoke(entity,new[]{observedVault});
        entity.MovementContext.SetRotation(new Vector2(q.Yaw,q.Pitch));
        p.Network.ObservedPlayers.Add(entity); p.Coop.Players.Add(q.Entity,entity);
        return entity;
        }
        catch { Remove(p,entity); throw; }
    }
    internal static void Remove(Plugin p,FikaPlayer entity)
    {
        if(entity==null) return;
        try
        {
        p.Guard(()=> { if(Singleton<Effects>.Instantiated) Singleton<Effects>.Instance.EffectsCommutator?.StopBleedingForPlayer(entity); });
        if(p.Coop?.Players.TryGetValue(entity.NetId,out var current)==true && current==entity) p.Coop.Players.Remove(entity.NetId);
        if(entity is ObservedPlayer observed) p.Network?.ObservedPlayers.Remove(observed);
        World?.UnregisterPlayer(entity);
        }
        finally
        {
        if(entity.PacketSender!=null) entity.PacketSender.SendState=false;
        entity.gameObject.SetActive(false);
        // EFT.Player.OnDestroy calls Dispose. Do not dispose here as well: native
        // movement/audio/controller cleanup is not guaranteed to be idempotent.
        UnityEngine.Object.Destroy(entity.gameObject);
        }
    }
    internal static void CleanupIncomplete(Plugin p,string prefix)
    {
        // Exceptional construction path only; never enumerate scene objects per frame.
        foreach(var entity in Resources.FindObjectsOfTypeAll<FikaPlayer>().Where(x=>x!=null && x.name.StartsWith(prefix,StringComparison.Ordinal)))
            p.Guard(()=>Remove(p,entity));
    }
}

internal sealed class ParkedEntity
{
    readonly Plugin p;
    readonly GameWorld world;
    readonly IFikaNetworkManager network;
    internal FikaPlayer Player { get; }
    readonly bool active,sendState,registered;
    readonly bool observed;
    bool closed;
    internal ParkedEntity(Plugin plugin,FikaPlayer player)
    {
        p=plugin; Player=player; world=Entities.World; network=p.Network;
        active=player.gameObject.activeSelf; registered=Entities.World.RegisteredPlayers.Contains(player);
        sendState=player.PacketSender?.SendState==true;
        observed=player is ObservedPlayer op && p.Network.ObservedPlayers.Contains(op);
        try
        {
        if(player.PacketSender!=null) player.PacketSender.SendState=false;
        if(player is ObservedPlayer o) p.Network.ObservedPlayers.Remove(o);
        p.Coop.Players.Remove(player.NetId);
        Entities.World.UnregisterPlayer(player);
        player.gameObject.SetActive(false);
        }
        catch
        {
            p.Guard(Restore); throw;
        }
    }
    internal void Restore()
    {
        if(closed || Player==null) return; closed=true;
        if(p.Network!=network || Entities.World!=world || world==null) return;
        Player.gameObject.SetActive(active);
        if(registered && !world.RegisteredPlayers.Contains(Player)) world.RegisterPlayer(Player);
        p.Coop.Players[Player.NetId]=Player;
        if(observed && !network.ObservedPlayers.Contains((ObservedPlayer)Player)) network.ObservedPlayers.Add((ObservedPlayer)Player);
        if(Player.PacketSender!=null) Player.PacketSender.SendState=sendState;
    }
    internal void Retire()
    {
        if(closed) return; closed=true;
        Entities.Remove(p,Player);
    }
}
