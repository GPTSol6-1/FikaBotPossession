using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using EFT;
using EFT.CameraControl;
using Fika.Core.Main.ClientClasses;
using Fika.Core.Main.Components;
using Fika.Core.Main.Players;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.Backend;
using Fika.Core.Networking.Packets.Generic.SubPackets;
using HarmonyLib;

namespace SptDiagnostics.Possession;

internal static class Hooks
{
    static readonly HashSet<PlayerCameraController> cleanedCameras=new();
    // Only set around our synchronous controller construction, never across awaits.
    internal static bool ReusingRaidCamera;
    static void Add(Harmony h,MethodBase target,string prefix)
    {
        if(target==null) throw new MissingMethodException(prefix+": required hook missing");
        h.Patch(target,prefix:new HarmonyMethod(typeof(Hooks),prefix));
    }
    internal static void Install(Harmony h)
    {
        Add(h,AccessTools.Method(typeof(BotOwner),"UpdateManual"),nameof(BotTick));
        Add(h,AccessTools.Method(typeof(BotOwner),"FixedUpdate"),nameof(BotTick));
        Add(h,AccessTools.Method(typeof(FikaServer),"OnLoadingProfilePacketReceived"),nameof(BindPeer));
        Add(h,AccessTools.Method(typeof(ObservedPlayer),"OnDead"),nameof(HumanDied));
        Add(h,AccessTools.Method(typeof(ClientExtractPacket),"Execute"),nameof(HumanExtracting));
        Add(h,AccessTools.Method(typeof(FikaPlayer),"LocalPlayerDiedAsync"),nameof(TemporaryDeath));
        h.Patch(AccessTools.DeclaredMethod(typeof(FikaPlayer),"CreateCorpse"),
            prefix:new HarmonyMethod(typeof(Hooks),nameof(PrepareTemporaryCorpse)),
            postfix:new HarmonyMethod(typeof(Hooks),nameof(TemporaryCorpseReady)));
        Add(h,AccessTools.Method(typeof(ClientHealthController),"TryProcessDownedState"),nameof(TemporaryDowned));
        Add(h,AccessTools.PropertyGetter(typeof(ClientHealthController),"CanBeDowned"),nameof(TemporaryDowned));
        Add(h,AccessTools.Method(typeof(CoopHandler),"SyncPlayersWithClients"),nameof(CharacterSync));
        Add(h,AccessTools.Method(typeof(CoopHandler),"ProcessQuitting"),nameof(RaidExit));
        Add(h,AccessTools.Method(typeof(PlayerCameraController),"OnDestroy"),nameof(CameraDestroyed));
        Add(h,AccessTools.Method(typeof(CameraManager),"SetCameraFromSettings"),nameof(KeepRaidCamera));
        Add(h,AccessTools.Method(typeof(Fika.Core.Main.FreeCamera.FreeCameraController),"ClearEffects"),nameof(SpectatorEffects));
        Add(h,AccessTools.Method(typeof(EffectsController),"OnClientPlayerEffectsPauseEvent"),nameof(PauseEffects));
        PossessionCompatibility.Install(h);
        var sain=AccessTools.TypeByName("SAIN.Components.BotComponent");
        if(sain!=null) Add(h,AccessTools.Method(sain,"ManualUpdate"),nameof(SainTick));
        var takeover=AccessTools.TypeByName("LiveRaid.Client.Spectator.TakeOver");
        if(takeover!=null) foreach(var t in new[]{typeof(BotOwner),typeof(Player),typeof(string)})
            Add(h,AccessTools.Method(takeover,"IsTaken",new[]{t}),nameof(LiveRaidTaken));
    }
    static bool BotTick(BotOwner __instance) => Plugin.Instance?.Host?.Taken(__instance)!=true;
    static bool CharacterSync() => Plugin.Instance?.Host?.Preparing!=true;
    static bool RaidExit() => Plugin.Instance?.Client?.AllowRaidExit()!=false;
    internal static void DestroyCamera(PlayerCameraController camera,bool constructed)
    {
        camera.enabled=false;
        // Clean up while the temporary singleton is current. The deferred Unity
        // callback must not release the restored spectator camera singleton.
        try
        {
            if(constructed) camera.OnDestroy();
            else
            {
                // Construct binds events synchronously. A failure can leave only
                // some unsubscribe delegates initialized; native OnDestroy calls
                // all of them unconditionally and throws before finishing.
                foreach(var name in new[]{"action_2","action_3","action_4","action_5","action_6"})
                    (AccessTools.Field(typeof(PlayerCameraController),name).GetValue(camera) as Action)?.Invoke();
            }
        }
        finally { cleanedCameras.Add(camera); PlayerCameraController.Destroy(camera.Player); }
    }
    static bool CameraDestroyed(PlayerCameraController __instance) => !cleanedCameras.Remove(__instance);
    static bool KeepRaidCamera() => !ReusingRaidCamera;
    static bool SpectatorEffects()
    {
        var p=Plugin.Instance;
        if(p?.Network==null || p.Headless || p.Allow?.Value!=true) return true;
        p.Guard(Presentation.Spectate);
        return false;
    }
    static bool PauseEffects(EffectsController __instance) => Plugin.Instance?.Network==null || Presentation.HasPlayer(__instance);
    static bool SainTick(object __instance)
    {
        var bot=AccessTools.Property(__instance.GetType(),"BotOwner")?.GetValue(__instance) as BotOwner;
        return Plugin.Instance?.Host?.Taken(bot)!=true;
    }
    static bool LiveRaidTaken(object __0,ref bool __result)
    {
        var host=Plugin.Instance?.Host;
        bool ours=host!=null && (__0 is BotOwner b ? host.Taken(b) : __0 is Player p ? host.Taken(p) : __0 is string id && host.Taken(id));
        if(ours) { __result=true; return false; } return true;
    }
    static void BindPeer(LoadingProfilePacket packet,NetPeer peer)
    {
        if(packet.Profiles?.Count==1) Plugin.Instance?.Bind(peer,packet.Profiles.First().Key.ProfileId);
    }
    static void HumanDied(ObservedPlayer __instance) => Plugin.Instance?.Remember(__instance,true);
    static void HumanExtracting(ClientExtractPacket __instance)
    {
        var p=Plugin.Instance;
        if(p?.Coop?.Players.TryGetValue(__instance.NetId,out var human)==true) p.Remember(human,false);
    }
    static bool TemporaryDeath(FikaPlayer __instance,ref Task __result)
    {
        if(Plugin.Instance?.Client?.IsTemporary(__instance)!=true) return true;
        __result=Task.CompletedTask; return false; // never mark the real human dead again through the backend
    }
    static void PrepareTemporaryCorpse(FikaPlayer __instance)
    {
        if(Plugin.Instance?.Client?.IsTemporary(__instance)!=true) return;
        // Native creation reuses the body's current render state. Freecam later
        // switches Coop.MyPlayer (the original human), not this temporary body.
        SetCorpseView(__instance);
    }
    internal static void SetCorpseView(FikaPlayer player)
    {
        player.PointOfView=EPointOfView.ThirdPerson;
        player.PlayerBody.PointOfView.Value=EPointOfView.ThirdPerson;
        player.PlayerBody.UpdatePlayerRenders(EPointOfView.ThirdPerson,player.Side);
    }
    static void TemporaryCorpseReady(FikaPlayer __instance,EFT.Interactive.Corpse __result)
    {
        var p=Plugin.Instance;
        if(p?.Client?.IsTemporary(__instance)!=true) return;
        p.Info($"Local corpse created for entity {__instance.NetId}: exists={__result!=null}, ragdoll={__result?.HasRagdoll}, body view={__instance.PlayerBody.PointOfView.Value}.");
    }
    static bool TemporaryDowned(ClientHealthController __instance,ref bool __result)
    {
        if(Plugin.Instance?.Client?.IsTemporary(__instance.Player)!=true) return true;
        // Fika's Kill prefix also checks CanBeDowned before entering Kill.
        // Blocking only TryProcessDownedState sends a peer death packet while
        // Kill itself (and therefore the owner's DiedEvent/OnDead) is skipped.
        __result=false; return false; // temporary bot bodies do not join the human revival roster
    }
}
