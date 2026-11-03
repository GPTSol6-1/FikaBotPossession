using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT;
using Fika.Core.Main.Players;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using UnityEngine;

namespace SptDiagnostics.Possession;

internal sealed class Host : IDisposable
{
    sealed class Session
    {
        internal NetPeer Owner;
        internal HandoffFrame Frame;
        internal TransferGate Gate;
        internal FikaBot Bot;
        internal AiPause Ai;
        internal ParkedEntity Parked;
        internal ObservedPlayer Replacement;
        internal BotsController Bots;
        internal bool Building;
        internal double Deadline,Heartbeat;
    }
    readonly Plugin p;
    readonly Dictionary<int,Session> sessions=new();
    readonly Dictionary<int,double> lastRequest=new();
    long generation=DateTime.UtcNow.Ticks;
    bool disposed;
    static double Now => Time.realtimeSinceStartupAsDouble;
    internal Host(Plugin plugin) { p=plugin; }
    FikaServer Server => (FikaServer)p.Network;
    int[] Peers { get { var peers=new List<NetPeer>(); Server.NetServer.GetConnectedPeers(peers); return peers.Select(x=>x.Id).ToArray(); } }
    internal bool Preparing => sessions.Values.Any(x=>x.Gate.State!=TransferGate.Stage.Active);
    internal bool Taken(BotOwner bot) => bot!=null && sessions.Values.Any(x=>x.Ai?.Owner==bot);
    internal bool Taken(Player player) => player!=null && sessions.Values.Any(x=>x.Bot==player || x.Replacement==player);
    internal bool Taken(string id) => sessions.Values.Any(x=>x.Bot?.ProfileId==id || x.Replacement?.ProfileId==id);
    internal void Receive(HandoffCommand q,NetPeer peer)
    {
        if(disposed || peer==null) return;
        if(q.Version!=HandoffCommand.Protocol) { Notice(peer,"Possession protocol mismatch; install the same current release on all clients."); return; }
        if(q.Kind==CommandKind.Acquire) { Acquire(q,peer); return; }
        var s=sessions.Values.FirstOrDefault(x=>x.Frame.Token==q.Token && x.Frame.Entity==q.Entity && x.Frame.Bot==q.Bot);
        if(s==null || !s.Gate.Participant(peer.Id)) return;
        if(q.Kind==CommandKind.Failed)
        {
            if(s.Gate.State!=TransferGate.Stage.Active || peer==s.Owner)
                End(s,"A participant ended or could not prepare the character: "+q.Message,false);
            return;
        }
        if(q.Kind==CommandKind.OwnerReady)
        {
            if(peer!=s.Owner || s.Gate.State!=TransferGate.Stage.Owner || s.Building) return;
            try
            {
                if(q.Controller==null || !System.Text.RegularExpressions.Regex.IsMatch(q.Controller,"\\A[0-9a-fA-F]{24}\\z"))
                    throw new FormatException("Invalid inventory controller ID");
                _=new MongoID(q.Controller);
            }
            catch(Exception) { End(s,"Invalid controller seed",false); return; }
            if(!s.Gate.OwnerReady(peer.Id,q.Token)) return;
            s.Frame.Controller=q.Controller; s.Frame.Operation=q.Operation;
            s.Building=true; _=PrepareObservers(s); return;
        }
        if(q.Kind==CommandKind.ObserverReady)
        {
            s.Gate.ObserverReady(peer.Id,q.Token); TryCommit(s); return;
        }
        if(peer!=s.Owner) return;
        if(q.Kind==CommandKind.Heartbeat && s.Gate.State==TransferGate.Stage.Active) s.Heartbeat=Now;
        if(q.Kind==CommandKind.Died && s.Gate.State==TransferGate.Stage.Active
            && s.Replacement!=null && !s.Replacement.HealthController.IsAlive) End(s,"Controlled character died",true);
    }
    void Acquire(HandoffCommand q,NetPeer peer)
    {
        if(lastRequest.TryGetValue(peer.Id,out var last) && Now-last<1) return;
        lastRequest[peer.Id]=Now;
        FikaPlayer requested=null;
        if(p.Coop!=null) p.Coop.Players.TryGetValue(q.Bot,out requested);
        p.Info($"Takeover request: human={q.Human}, bot={q.Bot}, raid={p.Coop?.LocalGameInstance?.GameController?.GameInstance.Status}, eligible={p.Eligible(peer,q.Human)}, candidate={requested?.GetType().Name}, alive={requested?.HealthController?.IsAlive}, AI state={requested?.AIData?.BotOwner?.BotState}.");
        if(!p.Headless) { Notice(peer,"Possession requires a dedicated headless host."); return; }
        if(p.Coop?.LocalGameInstance?.GameController?.GameInstance.Status!=GameStatus.Started)
        { Notice(peer,"Takeover requires an active raid."); return; }
        if(!p.Allow.Value || !p.Eligible(peer,q.Human)) { Notice(peer,"Takeover requires a dead or extracted human."); return; }
        if(sessions.ContainsKey(peer.Id) || sessions.Values.Any(x=>x.Frame.Bot==q.Bot))
        { Notice(peer,"That player or bot already has a handoff in progress."); return; }
        if(!p.Coop.Players.TryGetValue(q.Bot,out var candidate) || candidate is not FikaBot bot
            || !bot.HealthController.IsAlive || bot.UsedSimplifiedSkeleton || bot.AIData?.BotOwner==null
            || bot.AIData.BotOwner.BotState!=EBotState.Active || bot.BtrState!=EPlayerBtrState.Outside
            || bot.MovementContext.IsStationaryWeaponInHands || bot.Profile.Info.Settings.Role==WildSpawnType.marksman
            || bot.HandsController is not Player.FirearmController)
        { Notice(peer,"Select a living humanoid bot holding a firearm, outside a stationary weapon or vehicle."); return; }
        var frame=new HandoffFrame {Version=HandoffCommand.Protocol,Kind=FrameKind.PrepareOwner,Human=q.Human,Bot=q.Bot,
            Entity=Server.PopNetId(),Token=checked(++generation),X=bot.Position.x,Y=bot.Position.y,Z=bot.Position.z,
            Yaw=bot.Rotation.x,Pitch=bot.Rotation.y,Pose=bot.PoseLevel,Prone=bot.IsInPronePose,HeldItem=bot.HandsController.Item.Id};
        var s=new Session {Owner=peer,Frame=frame,Bot=bot,Bots=bot.AIData.BotOwner.BotsController,
            Gate=new TransferGate(peer.Id,frame.Token,Peers),Deadline=Now+45};
        sessions.Add(peer.Id,s);
        try
        {
            s.Ai=new AiPause(bot); s.Ai.Take();
            s.Frame.Payload=Entities.Snapshot(bot);
            s.Parked=new ParkedEntity(p,bot);
            p.Frame(peer,s.Frame);
            p.Info($"Handoff {frame.Token}: preparing bot {q.Bot} -> local entity {frame.Entity}, human {q.Human}.");
        }
        catch(Exception e) { p.Error(e); End(s,"Host could not freeze/snapshot this bot",false); }
    }
    async Task PrepareObservers(Session s)
    {
        try
        {
            var replacement=await Entities.Observe(p,s.Frame);
            if(s.Gate.State==TransferGate.Stage.Closed) { Entities.Remove(p,replacement); return; }
            s.Replacement=replacement;
            s.Frame.Kind=FrameKind.PrepareObservers;
            // Owner also sees this ordered message, but keeps its prepared local entity.
            p.Frame(null,s.Frame);
            s.Building=false; TryCommit(s);
        }
        catch(Exception e) { p.Error(e); End(s,"Host could not create the observed character",false); }
    }
    void TryCommit(Session s)
    {
        if(s.Building || s.Replacement==null || !s.Gate.AllReady) return;
        if(!p.Eligible(s.Owner,s.Frame.Human) || !s.Gate.RosterMatches(Peers))
        { End(s,"Participants changed during handoff",false); return; }
        try
        {
            // Remove the old bot from AI perception/population before registering
            // the new character. A new profile/net ID rejects delayed bot packets.
            if(!s.Gate.Commit()) throw new InvalidOperationException("Handoff commit rejected");
            s.Ai.Retire();
            s.Bots.BotSpawner.DeletePlayer(s.Bot);
            s.Bots.AddActivePLayer(s.Replacement);
            p.Info($"Possessed AI target {s.Frame.Entity}: IsAI={s.Replacement.IsAI}, observedAI={s.Replacement.IsObservedAI}, side={s.Replacement.Side}, registered={Entities.World.RegisteredPlayers.Contains(s.Replacement)}, group inherited={s.Replacement.GroupId==s.Bot.GroupId}.");
            s.Parked.Retire();
            s.Frame.Kind=FrameKind.Commit; s.Frame.Payload=Array.Empty<byte>();
            s.Heartbeat=Now; p.Frame(null,s.Frame);
            p.Info($"Handoff {s.Frame.Token}: committed entity {s.Frame.Entity}; local movement and native inventory enabled.");
        }
        catch(Exception e) { p.Error(e); End(s,"Host failed to commit the handoff",false); }
    }
    internal void Tick()
    {
        foreach(var s in sessions.Values.ToArray())
        {
            if(s.Gate.State!=TransferGate.Stage.Active)
            {
                if(!p.Allow.Value || p.Coop?.LocalGameInstance==null || Now>s.Deadline || !s.Gate.RosterMatches(Peers))
                    End(s,"Handoff disabled, timed out or participants changed",false);
                continue;
            }
            if(s.Replacement==null) End(s,"Controlled character is no longer present",false);
            else if(!s.Replacement.HealthController.IsAlive) End(s,"Controlled character died",true);
            else if(!p.Allow.Value || p.Coop?.LocalGameInstance?.GameController?.GameInstance.Status!=GameStatus.Started || Now-s.Heartbeat>12)
                End(s,"Control ended or controller heartbeat expired",false);
        }
    }
    void End(Session s,string reason,bool corpse)
    {
        if(s.Gate.State==TransferGate.Stage.Closed) return;
        bool committed=s.Gate.State==TransferGate.Stage.Active;
        s.Gate.Close(); sessions.Remove(s.Owner.Id);
        var frame=s.Frame; frame.Kind=committed?FrameKind.End:FrameKind.Abort;
        frame.KeepCorpse=committed && corpse; frame.Payload=Array.Empty<byte>(); frame.Message=reason;
        try { p.Frame(null,frame); } catch(Exception e) { p.Error(e); }
        if(!frame.KeepCorpse)
        {
            if(committed && s.Replacement!=null) p.Guard(()=>s.Bots.BotSpawner.DeletePlayer(s.Replacement));
            p.Guard(()=>Entities.Remove(p,s.Replacement));
        }
        if(!committed)
        {
            try { s.Parked?.Restore(); } catch(Exception e) { p.Error(e); }
            try { s.Ai?.Dispose(); } catch(Exception e) { p.Error(e); }
        }
        else
        {
            p.Guard(()=>s.Ai?.Retire());
            p.Guard(()=>s.Parked?.Retire());
        }
        p.Info($"Handoff {frame.Token}: {(committed?"ended":"rolled back")}: {reason}; corpse={frame.KeepCorpse}.");
    }
    void Notice(NetPeer peer,string text)
    {
        p.Info("Takeover rejected: "+text);
        p.Frame(peer,new HandoffFrame {Kind=FrameKind.Notice,Message=text});
    }
    internal void Disconnect(NetPeer peer)
    {
        foreach(var s in sessions.Values.ToArray())
            if(s.Owner==peer || (s.Gate.State!=TransferGate.Stage.Active && s.Gate.Participant(peer.Id)))
                End(s,"Participant disconnected",false);
        lastRequest.Remove(peer.Id);
    }
    public void Dispose()
    {
        disposed=true;
        foreach(var s in sessions.Values.ToArray()) End(s,"Raid/session ended",false);
    }
}

internal sealed class AiPause : IDisposable
{
    readonly FikaBot player;
    readonly BotOwner bot;
    internal BotOwner Owner => bot;
    readonly List<Behaviour> paused=new();
    bool detached,retired;
    internal AiPause(FikaBot value) { player=value; bot=value.AIData.BotOwner; }
    internal void Take()
    {
        if(bot.Brain?.Agent==null || bot.BotsController?.AICoreController==null) throw new InvalidOperationException("Bot has no AI node");
        bot.Mover.Stop(); player.Move(Vector2.zero); player.EnableSprint(false);
        if(player.HandsController is Player.FirearmController firearm) { firearm.SetTriggerPressed(false); firearm.SetAim(false); }
        bot.BotsController.AICoreController.RemoveNodeController(bot.Brain.Agent); detached=true;
        foreach(var b in player.GetComponents<MonoBehaviour>())
            if(b.enabled && (b.GetType().Name=="LootingBrain" || b.GetType().Name=="BotObjectiveManager")) { b.enabled=false; paused.Add(b); }
    }
    internal void Retire()
    {
        if(retired) return;
        if(bot!=null)
        {
            // DeletePlayer removes a perception target, not an AI group member.
            // Native BotDied updates population and removes the BotOwner through
            // BotsList.Remove -> BotsGroup.RemoveAlly before its Transform dies.
            bot.BotsController.BotDied(bot);
            bot.Deactivate();
        }
        retired=true;
    }
    public void Dispose()
    {
        if(retired) return; retired=true;
        try
        {
            if(detached && bot!=null && !bot.IsDead && bot.Brain?.Agent!=null)
            {
                var core=bot.BotsController.AICoreController;
                core._listToDel.Remove(bot.Brain.Agent); core.AddNodeController(bot.Brain.Agent);
            }
        }
        finally { foreach(var b in paused) if(b!=null) b.enabled=true; }
    }
}
