using System;
using System.Collections.Generic;
using EFT;
using Fika.Core.Main.ClientClasses;
using Fika.Core.Main.Players;
using HarmonyLib;

namespace SptDiagnostics.Possession;

internal static class RuntimeContract
{
    internal static void Validate()
    {
        foreach(var name in new[]{"GetSensitivity","GetAimingSensitivity","_vaultingComponent","_vaultingComponentDebug",
            "_vaultingParameters","_vaultingGameplayRestrictions"}) RequireField(typeof(Player),name);
        foreach(var name in new[]{"_insurance","_gameDateTime","_location","_myPlayer"}) RequireField(typeof(GamePlayerOwner),name);
        RequireField(typeof(ObservedPlayer),"_observedVaultingParameters");
        RequireField(typeof(ClientQuestController),"_canSendAndReceive");
        RequireField(typeof(EFT.CameraControl.CameraManager),"_occlusionCullingEnabled");
        RequireField(typeof(Fika.Core.Main.FreeCamera.FreeCameraController),"_hasEnabledCulling");
        RequireField(typeof(Fika.Core.Main.FreeCamera.FreeCameraController),"_extractText");
        RequireField(typeof(EffectsController),"player");
        RequireField(typeof(EffectsController),"_effectAccumulators");
        foreach(var name in new[]{"action_2","action_3","action_4","action_5","action_6"})
            RequireField(typeof(EFT.CameraControl.PlayerCameraController),name);
        var accepted=AccessTools.Field(typeof(ClientSharedQuestController),"_acceptedTypes");
        if(accepted==null || accepted.FieldType!=typeof(HashSet<string>))
            throw new MissingFieldException("Shared quest isolation contract changed");
        if(AccessTools.Method(typeof(Player),"InitVaultingAudioControllers")==null)
            throw new MissingMethodException("Observed vault audio initializer is missing");
    }
    static void RequireField(Type type,string name)
    {
        if(AccessTools.Field(type,name)==null) throw new MissingFieldException(type.FullName,name);
    }
}
