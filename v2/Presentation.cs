using System;
using System.Collections.Generic;
using EFT;
using EFT.CameraControl;
using HarmonyLib;

namespace SptDiagnostics.Possession;

// Fika normally destroys these components once the human becomes a spectator.
// Possession needs them again, on the same camera that owns graphics/freecam.
internal static class Presentation
{
    internal static void Detach(EffectsController effects)
    {
        if(effects==null) return;
        var field=AccessTools.Field(typeof(EffectsController),"player");
        var player=field.GetValue(effects) as Player;
        // Accumulator.Toggle can read the bound player's skills even when an
        // effect is being removed. Clear before erasing that player reference.
        foreach(var accumulator in (List<EffectsController.EffectAccumulator>)AccessTools.Field(typeof(EffectsController),"_effectAccumulators").GetValue(effects))
            while(accumulator.ActiveEffects.Count>0)
                accumulator.DeleteEffect(accumulator.ActiveEffects[0]);
        if(player!=null)
        {
            player.HealthController.EffectStartedEvent-=effects.OnEffectStarted;
            player.HealthController.EffectRemovedEvent-=effects.OnEffectRemoved;
            player.OnDamageReceived-=effects.OnPlayerDamaged;
            player.HealthController.ApplyDamageEvent-=effects.OnHealthApplyDamage;
            player.HealthController.DiedEvent-=effects.OnHealthDied;
            player.HealthController.BurnEyesEvent-=effects.OnHealthBurnEyes;
            player.HealthController.StimulatorBuffEvent-=effects.OnStimulatorBuff;
            player.OnGlassesChanged-=effects.OnGlassesChanged;
        }
        field.SetValue(effects,null);
    }
    internal static void Spectate()
    {
        var manager=CameraManager.Instance;
        var effects=manager.EffectsController;
        Detach(effects);
        if(effects!=null)
        {
            effects.OnGlassesChanged(null,false);
            effects.enabled=false;
        }
        if(manager.VisorEffect!=null) manager.VisorEffect.Clear();
        BindVisor();
        if(manager.NightVision!=null && manager.NightVision.On) manager.NightVision.Switch(false);
        if(manager.ThermalVision!=null && manager.ThermalVision.On) manager.ThermalVision.Switch(false);
        manager.Camera.GetComponent<DeathFade>()?.DisableEffect();
        manager.Camera.GetComponent<FastBlur>()?.Reset();
    }
    internal static void BindVisor()
    {
        var manager=CameraManager.Instance;
        if(manager.VisorSwitcher==null || manager.VisorEffect==null)
            throw new InvalidOperationException("Spectator visor components are missing; start a fresh raid with this version enabled.");
        manager.VisorSwitcher.Init(x=>manager.VisorEffect.SetIntensity(x));
    }
    internal static void Begin()
    {
        var effects=CameraManager.Instance.EffectsController;
        if(effects==null) throw new InvalidOperationException("Spectator effects were destroyed; start a fresh raid with this version enabled.");
        Detach(effects);
        BindVisor();
        effects.enabled=true;
    }
    internal static bool HasPlayer(EffectsController effects) => AccessTools.Field(typeof(EffectsController),"player").GetValue(effects) is Player player && player!=null;
}
