using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommonAssets.Scripts.Game;
using Comfort.Common;
using Diz.Jobs;
using EFT;
using EFT.CameraControl;
using EFT.InputSystem;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Insurance;
using Fika.Core.Main.ClientClasses;
using Fika.Core.Main.FreeCamera;
using Fika.Core.Main.ObservedClasses;
using Fika.Core.Main.Players;
using Fika.Core.Main.Utils;
using HarmonyLib;
using JsonType;
using UnityEngine;
using FikaCamera=Fika.Core.Main.FreeCamera.FreeCamera;

namespace SptDiagnostics.Possession;

internal sealed class LocalCharacter : IDisposable
{
    readonly Plugin p;
    readonly HandoffFrame frame;
    internal FikaPlayer Player { get; private set; }
    internal FikaPlayer Original { get; }
    internal bool Active { get; private set; }
    readonly EftGamePlayerOwner originalOwner;
    readonly CancellationTokenSource cancellation=new();
    PossessionOwner owner;
    PlayerCameraController oldCamera,newCamera;
    bool oldCameraEnabled,freeControllerEnabled,freeActive,disposed,parked,presentationCaptured,originalInput;
    FreeCameraController freeController;
    FikaCamera freeCamera;
    Camera camera;
    Transform cameraParent;
    Vector3 cameraPosition;
    Quaternion cameraRotation;
    float fov;
    CursorLockMode cursorLock;
    bool cursorVisible;
    bool changedMapCulling;
    bool? occlusionCulling;
    GameObject extractPrompt;
    bool extractPromptActive;
    internal LocalCharacter(Plugin plugin,HandoffFrame q)
    {
        p=plugin; frame=q; Original=p.Coop.MyPlayer;
        originalOwner=Original?.GetComponent<EftGamePlayerOwner>();
        if(originalOwner==null || originalOwner.Session is not IEftSession)
            throw new InvalidOperationException("Original raid player/input owner is unavailable");
    }
    internal async Task Prepare()
    {
        var data=Entities.Decode(frame.Payload);
        var resources=data.Profile.GetAllPrefabPaths(true).ToArray();
        await Singleton<ObjectsFactory>.Instance.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid,
            ObjectsFactory.AssemblyType.Local,resources,JobYieldPriority.General,null,cancellation.Token);
        cancellation.Token.ThrowIfCancellationRequested();
        string nickname=data.Profile.Nickname;
        var player=await FikaPlayer.Create(Entities.World,frame.Entity,Entities.Position(frame),Quaternion.Euler(0,frame.Yaw,0),
            "Player","PossessionLocal_"+frame.Entity+"_",EPointOfView.FirstPerson,data.Profile,false,EUpdateQueue.Update,
            EFT.Player.EUpdateMode.Auto,EFT.Player.EUpdateMode.Auto,AppEnvironment.Config.CharacterController.ClientPlayerMode,
            Field<Func<float>>(Original,"GetSensitivity"),Field<Func<float>>(Original,"GetAimingSensitivity"),new ObservedStatisticsManager(),
            new ClientViewFilter(),(IEftSession)originalOwner.Session,frame.Entity,p.Network.StrictInventorySync);
        Player=player;
        if(disposed || cancellation.IsCancellationRequested)
        {
            Entities.Remove(p,player); Player=null; throw new OperationCanceledException();
        }
        // Fika's normal factory uses the human nickname. Retain the bot identity.
        player.Profile.Info.SetProfileNickname(nickname);
        // Normal raid spawning assigns Location after Create; mid-raid bodies
        // bypass that path. Dynamic Maps uses it to select the current map.
        player.Location=Original.Location;
        if(player.QuestController is ClientQuestController quests)
        {
            quests.ToggleSend(false);
            AccessTools.Field(typeof(ClientQuestController),"_canSendAndReceive")?.SetValue(quests,false);
            // Shared-quest find-item broadcasts must not originate from this body.
            if(quests is ClientSharedQuestController shared)
                (AccessTools.Field(typeof(ClientSharedQuestController),"_acceptedTypes")?.GetValue(shared) as System.Collections.Generic.HashSet<string>)?.Clear();
        }
        player.MovementContext.SetRotation(new Vector2(frame.Yaw,frame.Pitch));
        Entities.World.UnregisterPlayer(player); parked=true;
        player.gameObject.SetActive(false);
        Entities.World.MainPlayer=Original;
        p.Info($"Local entity {frame.Entity} prepared: {player.InventoryController.GetType().Name}; {player.HealthController.GetType().Name}.");
    }
    internal void Start()
    {
        if(disposed || Player==null || Active) throw new InvalidOperationException("Local body is not ready");
        camera=CameraManager.Instance.Camera;
        cameraParent=camera.transform.parent; cameraPosition=camera.transform.localPosition; cameraRotation=camera.transform.localRotation;
        fov=camera.fieldOfView; cursorLock=Cursor.lockState; cursorVisible=Cursor.visible;
        presentationCaptured=true;
        occlusionCulling=Field<bool?>(CameraManager.Instance,"_occlusionCullingEnabled");
        oldCamera=Original.GetComponent<PlayerCameraController>();
        if(oldCamera!=null) { oldCameraEnabled=oldCamera.enabled; oldCamera.enabled=false; }
        freeController=Singleton<FreeCameraController>.Instance;
        if(freeController!=null)
        {
            freeControllerEnabled=freeController.enabled;
            var text=Field<Component>(freeController,"_extractText");
            if(text!=null)
            {
                extractPrompt=text.gameObject; extractPromptActive=extractPrompt.activeSelf;
                extractPrompt.SetActive(false);
            }
            // Fika's map triggers and PerfectCulling groups are separate from
            // Unity occlusion. Repeated Disable calls lose its restore snapshot.
            if(!Field<bool>(freeController,"_hasEnabledCulling"))
            {
                changedMapCulling=true;
                freeController.DisableAllCullingObjects();
            }
            freeController.enabled=false;
        }
        freeCamera=camera.GetComponent<FikaCamera>();
        if(freeCamera!=null) { freeActive=freeCamera.IsActive; freeCamera.SetActive(false,false); }
        originalInput=originalOwner.InputTree.Contains(originalOwner); originalOwner.Mute(true);
        Player.gameObject.SetActive(true);
        if(parked) { Entities.World.RegisterPlayer(Player); parked=false; }
        p.Coop.Players[frame.Entity]=Player;
        // Keep Coop.MyPlayer and CoopGame.LocalPlayer pointing at the original raid
        // player. Only packet callbacks, world presentation and input follow the body.
        p.Network.SetupGameVariables(Player);
        Entities.World.MainPlayer=Player;
        var ui=UnityEngine.Object.FindObjectOfType<GameUI>(true);
        if(ui==null) throw new InvalidOperationException("Raid GameUI is unavailable");
        owner=EftGamePlayerOwner.Create<PossessionOwner>(Player,originalOwner.InputTree,
            Field<InsuranceCompany>(originalOwner,"_insurance"),(IEftSession)originalOwner.Session,ui,
            Field<GameDateTime>(originalOwner,"_gameDateTime"),Field<LocationSettings.Location>(originalOwner,"_location"));
        owner.OnLeave+=OnLeave;
        owner.Original=Original;
        Presentation.Begin();
        // Construct normally to bind player presentation, but retain the live
        // camera, post-processing and Fika spectator components on that camera.
        Hooks.ReusingRaidCamera=true;
        try { newCamera=PlayerCameraController.Create(Player); }
        finally { Hooks.ReusingRaidCamera=false; }
        if(CameraManager.Instance.Camera!=camera)
            throw new InvalidOperationException("Raid camera changed during possession activation");
        Player.PointOfView=EPointOfView.FirstPerson;
        newCamera.UpdatePointOfView();
        CameraManager.Instance.IsActive=true;
        CameraManager.Instance.SetOcclusionCullingEnabled(true);
        Active=true;
        // Normal Fika characters start health effects in CoopGame.Spawn. This
        // temporary body is created mid-raid and never visits that spawn path.
        var health=(ClientHealthController)Player.ActiveHealthController;
        health.Start();
        p.Info($"Health simulation started for entity {frame.Entity}: {health._started}.");
        Player.MovementContext.SetPoseLevel(frame.Pose);
        if(frame.Prone && !Player.IsInPronePose) Player.ToggleProne();
        Player.PacketSender.Init();
        owner.vmethod_0();
        owner.Mute(!Application.isFocused);
        p.Guard(()=>PossessionCompatibility.Begin(Player,Original));
        // Initial equip uses the same network request/callback path as normal play.
        var item=Player.Inventory.Equipment.GetAllItems().FirstOrDefault(x=>x.Id.ToString()==frame.HeldItem);
        if(item is Weapon weapon) Player.Proceed(weapon,null,false);
        p.Info($"Local control started for entity {frame.Entity}; native input, camera and raid inventory owner attached.");
    }
    static T Field<T>(object value,string name)
    {
        var f=AccessTools.Field(value.GetType(),name) ?? throw new MissingFieldException(value.GetType().FullName,name);
        return (T)f.GetValue(value);
    }
    void OnLeave()
    {
        // The temporary body's menu must not invoke CoopGame's original raid-exit
        // handler. Leaving ends this body's participation; it cannot return to AI.
        p.Client?.RequestEnd();
    }
    internal void Focus(bool focused)
    {
        if(!Active || Player==null) return;
        owner?.Mute(!focused);
        if(!focused)
        {
            Player.Move(Vector2.zero); Player.EnableSprint(false);
            if(Player.HandsController is EFT.Player.FirearmController firearm) firearm.SetTriggerPressed(false);
        }
    }
    internal void Restore(bool keepCorpse,bool raidEnding=false)
    {
        if(disposed) return; disposed=true; cancellation.Cancel();
        // Every restoration is attempted even if one subsystem has already torn down.
        void Try(Action action) { try { action(); } catch(Exception e) { p.Error(e); } }
        Try(PossessionCompatibility.Restore);
        if(owner!=null)
        {
            Try(()=>owner.Mute(true)); Try(()=>owner.vmethod_1());
            Try(()=>owner.CleanupOnDestroy());
            UnityEngine.Object.Destroy(owner); owner=null;
        }
        if(Player!=null && Player.PacketSender!=null) Player.PacketSender.SendState=false;
        bool cameraConstructed=newCamera!=null;
        if(newCamera==null && presentationCaptured && Player!=null) newCamera=Player.GetComponent<PlayerCameraController>();
        if(presentationCaptured && !raidEnding) Try(()=>Presentation.Detach(CameraManager.Instance.EffectsController));
        if(newCamera!=null)
        {
            Try(()=>Hooks.DestroyCamera(newCamera,cameraConstructed));
        }
        if(Original!=null && Entities.World!=null)
        {
            Try(()=>Entities.World.MainPlayer=Original);
            if(presentationCaptured && !raidEnding) Try(()=>CameraManager.Instance.SetPlayer(Original));
            if(!raidEnding) Try(()=>p.Network?.SetupGameVariables(Original));
            Try(()=>AccessTools.Field(typeof(GamePlayerOwner),"_myPlayer").SetValue(null,Original));
            if(!raidEnding) Try(()=>Singleton<BetterAudio>.Instance.SetProtagonist(Original));
        }
        if(oldCamera!=null && !raidEnding)
        {
            Try(()=> { oldCamera.enabled=oldCameraEnabled; Singleton<PlayerCameraController>.Create(oldCamera); });
        }
        if(camera!=null && !raidEnding)
        {
            Try(()=> { camera.fieldOfView=fov; camera.transform.SetParent(cameraParent,false);
                camera.transform.localPosition=cameraPosition; camera.transform.localRotation=cameraRotation; });
        }
        if(freeCamera!=null && !raidEnding) Try(()=>freeCamera.SetActive(freeActive,p.Coop?.LocalGameInstance?.ExtractedPlayers.Contains(Original.NetId)==true));
        if(camera!=null && presentationCaptured && !raidEnding)
        {
            // Controller cleanup deinitializes shared visor/audio bindings even
            // though this camera now belongs to the spectator again.
            Try(Presentation.Spectate);
            Try(()=>Singleton<AudioListenerConsistencyManager>.Instance.Follow(camera.transform));
            Try(()=>camera.GetComponent<DeathFade>()?.DisableEffect());
            Try(()=>CameraManager.Instance.SetOcclusionCullingEnabled(occlusionCulling));
        }
        if(changedMapCulling && freeController!=null && !raidEnding) Try(()=>freeController.EnableAllCullingObjects());
        if(freeController!=null && !raidEnding) freeController.enabled=freeControllerEnabled;
        if(extractPrompt!=null && !raidEnding) extractPrompt.SetActive(extractPromptActive);
        if(presentationCaptured && !raidEnding) { Cursor.lockState=cursorLock; Cursor.visible=cursorVisible; }
        if(presentationCaptured && originalOwner!=null && !raidEnding) Try(()=>originalOwner.Mute(!originalInput));
        Active=false;
        if(keepCorpse && Player!=null && !raidEnding)
        {
            var corpse=Player.GetComponent<EFT.Interactive.Corpse>();
            p.Info($"Local corpse retained for entity {frame.Entity}: exists={corpse!=null}, ragdoll={corpse?.HasRagdoll}, body view={Player.PlayerBody.PointOfView.Value}.");
        }
        if(presentationCaptured && !raidEnding) p.Info($"Spectator presentation restored; camera retained={camera!=null && CameraManager.Exist && CameraManager.Instance.Camera==camera}, freecam={freeCamera?.IsActive}, map culling restored={changedMapCulling}.");
        if(raidEnding) p.Info("Possession cleaned up during raid teardown; spectator UI was not reactivated.");
        if(!keepCorpse && Player!=null) { Try(()=>Entities.Remove(p,Player)); Player=null; }
    }
    public void Dispose() => Restore(false);
}

// Normal EFT movement/weapon translators and the normal raid inventory screen.
// The surrounding CoopGame retains its original PlayerOwner for raid saving.
public sealed class PossessionOwner : EftGamePlayerOwner
{
    internal Player Original;
    bool cleaned;
    public override bool SetItemInHandsImmediately => false;
    public override void CleanupOnDestroy()
    {
        if(cleaned) return; cleaned=true;
        try { base.CleanupOnDestroy(); }
        finally
        {
            if(Original!=null) AccessTools.Field(typeof(GamePlayerOwner),"_myPlayer").SetValue(null,Original);
        }
    }
}
