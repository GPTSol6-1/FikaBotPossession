using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT;
using EFT.CameraControl;
using Fika.Core.Main.Players;
using UnityEngine;
using FikaCamera=Fika.Core.Main.FreeCamera.FreeCamera;

namespace SptDiagnostics.Possession;

internal sealed class Client
{
    sealed class Transfer
    {
        internal HandoffFrame Frame;
        internal ParkedEntity Old;
        internal LocalCharacter Local;
        internal ObservedPlayer Remote;
        internal bool Closed,Committed;
        internal float Started=Time.unscaledTime;
    }
    readonly Plugin p;
    readonly Dictionary<long,Transfer> transfers=new();
    readonly HashSet<long> completed=new();
    internal LocalCharacter Controlled { get; private set; }
    Transfer mine;
    bool pending;
    bool tearingDown;
    internal bool BlockRaidExit => mine!=null;
    internal bool AllowRaidExit()
    {
        if(!BlockRaidExit) return true;
        if(Fika.Core.FikaPlugin.Instance.Settings.ExtractKey.Value.IsDown())
            Note($"Press {p.EndKey.Value} to end possession, then use spectator extraction.");
        return false;
    }
    float pendingUntil,nextHeartbeat,messageUntil;
    string message="";
    internal Client(Plugin plugin) { p=plugin; }
    internal bool IsTemporary(Player player) => player!=null && transfers.Values.Any(x=>x.Local?.Player==player);
    internal void Receive(HandoffFrame q)
    {
        if(q.Version!=HandoffCommand.Protocol) { Note("Possession version mismatch"); return; }
        if(q.Kind==FrameKind.Notice) { pending=false; Note(q.Message); return; }
        if(q.Token==0 || completed.Contains(q.Token)) return;
        if(q.Kind==FrameKind.PrepareOwner)
        {
            if(transfers.ContainsKey(q.Token)) return;
            if(!pending || mine!=null || !p.Eligible(p.Coop?.MyPlayer) || q.Human!=p.Coop.MyPlayer.NetId)
            { Send(q,CommandKind.Failed,"Controller is no longer an eligible spectator"); return; }
            pending=false;
            var t=new Transfer {Frame=q}; transfers.Add(q.Token,t); mine=t;
            _=Prepare(t,true); return;
        }
        if(q.Kind==FrameKind.PrepareObservers)
        {
            if(transfers.ContainsKey(q.Token)) return; // selected owner is already ready
            var t=new Transfer {Frame=q}; transfers.Add(q.Token,t); _=Prepare(t,false); return;
        }
        if(!transfers.TryGetValue(q.Token,out var transfer))
        { if(q.Kind==FrameKind.Abort || q.Kind==FrameKind.End) completed.Add(q.Token); return; }
        if(q.Kind==FrameKind.Commit)
        {
            if(transfer.Committed) return;
            try
            {
                transfer.Committed=true;
                transfer.Old?.Retire();
                if(transfer.Local!=null)
                {
                    Controlled=transfer.Local; Controlled.Start(); nextHeartbeat=0;
                    Note($"Bot takeover active. {p.EndKey.Value}: kill possessed character and return to freecam.");
                }
                p.Info($"Handoff {q.Token}: client committed entity {q.Entity}.");
            }
            catch(Exception e) { p.Error(e); Send(q,CommandKind.Failed,"Client activation failed"); Close(transfer,false,false); }
        }
        else if(q.Kind==FrameKind.Abort || q.Kind==FrameKind.End)
        { Close(transfer,q.Kind==FrameKind.Abort,q.KeepCorpse); Note(q.Message); }
    }
    async Task Prepare(Transfer t,bool local)
    {
        try
        {
            if(!p.Coop.Players.TryGetValue(t.Frame.Bot,out var old) || old is not ObservedPlayer)
                throw new InvalidOperationException("Selected bot replica is not loaded on this client");
            t.Old=new ParkedEntity(p,old);
            if(local)
            {
                t.Local=new LocalCharacter(p,t.Frame);
                await t.Local.Prepare();
                if(t.Closed) { t.Local.Dispose(); return; }
                var q=Command(t.Frame,CommandKind.OwnerReady);
                q.Controller=t.Local.Player.InventoryController.CurrentId.ToString();
                q.Operation=t.Local.Player.InventoryController.NextOperationId;
                p.Send(q);
            }
            else
            {
                var entity=await Entities.Observe(p,t.Frame);
                if(t.Closed) { Entities.Remove(p,entity); return; }
                t.Remote=entity; Send(t.Frame,CommandKind.ObserverReady);
            }
        }
        catch(Exception e)
        {
            p.Error(e);
            if(local && t.Local?.Player==null) Entities.CleanupIncomplete(p,"PossessionLocal_"+t.Frame.Entity+"_");
            if(!t.Closed) { Send(t.Frame,CommandKind.Failed,e.GetBaseException().Message); Close(t,true,false); }
        }
    }
    void Close(Transfer t,bool rollback,bool corpse)
    {
        if(t.Closed) return; t.Closed=true; completed.Add(t.Frame.Token); transfers.Remove(t.Frame.Token);
        if(mine==t) { mine=null; Controlled=null; pending=false; }
        try { t.Local?.Restore(corpse,tearingDown); } catch(Exception e) { p.Error(e); }
        if(!corpse && t.Remote!=null) p.Guard(()=>Entities.Remove(p,t.Remote));
        if(rollback && !t.Committed) p.Guard(()=>t.Old?.Restore());
        else p.Guard(()=>t.Old?.Retire());
        p.Info($"Handoff {t.Frame.Token}: client closed; rollback={rollback}, corpse={corpse}.");
    }
    internal void Tick()
    {
        if(p.Network==null || p.Headless) return;
        bool takePressed=Input.GetKeyDown(p.TakeKey.Value);
        if(takePressed)
            p.Info($"Takeover key: eligible={p.Eligible(p.Coop?.MyPlayer)}, freecam={(CameraManager.Exist ? CameraManager.Instance.Camera?.GetComponent<FikaCamera>()?.IsActive : null)}, pending={pending}, transfer={mine!=null}, raid={p.Coop?.LocalGameInstance?.GameController?.GameInstance.Status}.");
        foreach(var t in transfers.Values.Where(x=>!x.Committed && Time.unscaledTime-x.Started>55).ToArray())
        { Send(t.Frame,CommandKind.Failed,"Client preparation timed out"); Close(t,true,false); }
        if(pending && Time.unscaledTime>pendingUntil) { pending=false; Note("No handoff response from host."); }
        if(mine!=null)
        {
            if(mine.Committed && Application.isFocused && p.EndKey.Value.IsDown()) KillControlled();
            if(mine.Committed && Time.unscaledTime>=nextHeartbeat)
            {
                nextHeartbeat=Time.unscaledTime+1;
                Send(mine.Frame,Controlled?.Player?.HealthController.IsAlive==false ? CommandKind.Died : CommandKind.Heartbeat);
            }
            return;
        }
        if(!pending && p.Eligible(p.Coop?.MyPlayer) && CameraManager.Exist
            && CameraManager.Instance.Camera.GetComponent<FikaCamera>()?.IsActive==true && takePressed) Acquire();
    }
    void Acquire()
    {
        if(p.IsHost) { Note("This version requires a dedicated headless host."); return; }
        var camera=CameraManager.Instance.Camera;
        var bot=p.Coop.Players.Values.Where(x=>x!=null && x.IsObservedAI && x.HealthController.IsAlive)
            .Select(x=>new {Player=x,Screen=camera.WorldToViewportPoint(x.Position+Vector3.up)})
            .Where(x=>x.Screen.z>0 && Mathf.Abs(x.Screen.x-.5f)<.15f && Mathf.Abs(x.Screen.y-.5f)<.2f)
            .OrderBy(x=>(new Vector2(x.Screen.x-.5f,x.Screen.y-.5f)).sqrMagnitude).FirstOrDefault()?.Player;
        if(bot==null) { Note("Aim the spectator camera at a living bot."); return; }
        pending=true; pendingUntil=Time.unscaledTime+50;
        p.Send(new HandoffCommand {Kind=CommandKind.Acquire,Human=p.Coop.MyPlayer.NetId,Bot=bot.NetId});
        Note("Preparing local player handoff…");
    }
    internal void Focus(bool focused) => Controlled?.Focus(focused);
    void KillControlled()
    {
        var body=Controlled?.Player;
        if(mine?.Committed!=true || Controlled?.Active!=true || body?.HealthController.IsAlive!=true) return;
        p.Info($"F7 death requested for temporary entity {body.NetId}; retaining native corpse and inventory.");
        // Let native death create/synchronize the corpse before the existing
        // host death monitor closes the handoff. Failed would despawn it.
        body.ActiveHealthController.Kill(EDamageType.Undefined);
    }
    internal void RequestEnd()
    {
        if(mine!=null)
        {
            Send(mine.Frame,CommandKind.Failed,"Controller left possession; temporary body retired");
            Note("Ending possession; waiting for host. Body and items will despawn.");
        }
    }
    static HandoffCommand Command(HandoffFrame q,CommandKind kind) => new()
        {Kind=kind,Human=q.Human,Bot=q.Bot,Entity=q.Entity,Token=q.Token};
    void Send(HandoffFrame q,CommandKind kind,string text="")
    {
        var command=Command(q,kind); command.Message=text.Length>500 ? text.Substring(0,500) : text; p.Send(command);
    }
    void Note(string text) { message=text; messageUntil=Time.unscaledTime+8; p.Info("Possession notice: "+text); }
    internal void Draw()
    {
        string text=Time.unscaledTime<messageUntil && message.Length>0 ? message : "";
        if(Controlled?.Active==true)
            text=$"{p.EndKey.Value}: kill possessed character and return to freecam (body remains lootable).";
        if(text.Length==0 && p.Network!=null && !p.Headless && mine==null && !pending && p.Eligible(p.Coop?.MyPlayer)
            && CameraManager.Exist && CameraManager.Instance.Camera.GetComponent<FikaCamera>()?.IsActive==true)
            text=$"Aim at a living bot and press {p.TakeKey.Value} to take control.";
        if(text.Length==0) return;
        var style=new GUIStyle(GUI.skin.box) {alignment=TextAnchor.MiddleLeft,fontSize=16,wordWrap=true};
        style.normal.textColor=Color.white;
        GUI.Box(new Rect(20,20,Mathf.Min(1000,Screen.width-40),55),"Fika Bot Possession: "+text,style);
    }
    internal void Reset()
    {
        tearingDown=true;
        try
        {
            foreach(var t in transfers.Values.ToArray()) Close(t,!t.Committed,false);
            completed.Clear(); pending=false;
        }
        finally { tearingDown=false; }
    }
}
