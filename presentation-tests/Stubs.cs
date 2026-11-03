using System;
using System.Collections.Generic;
using System.Reflection;
namespace HarmonyLib {
 static class AccessTools { public static FieldInfo Field(Type type,string name)=>type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic); }
}
namespace EFT {
 public class Player {
  public Health HealthController=new();
  public event Action OnDamageReceived,OnGlassesChanged;
  public int Listeners => (OnDamageReceived?.GetInvocationList().Length??0)+(OnGlassesChanged?.GetInvocationList().Length??0)+HealthController.Listeners;
  public void Damage()=>OnDamageReceived?.Invoke();
 }
 public class Health {
  public event Action EffectStartedEvent,EffectRemovedEvent,ApplyDamageEvent,DiedEvent,BurnEyesEvent,StimulatorBuffEvent;
  public int Listeners => (EffectStartedEvent?.GetInvocationList().Length??0)+(EffectRemovedEvent?.GetInvocationList().Length??0)+(ApplyDamageEvent?.GetInvocationList().Length??0)+(DiedEvent?.GetInvocationList().Length??0)+(BurnEyesEvent?.GetInvocationList().Length??0)+(StimulatorBuffEvent?.GetInvocationList().Length??0);
  public void Signal(){EffectStartedEvent?.Invoke();EffectRemovedEvent?.Invoke();ApplyDamageEvent?.Invoke();DiedEvent?.Invoke();BurnEyesEvent?.Invoke();StimulatorBuffEvent?.Invoke();}
 }
}
namespace EFT.CameraControl {
 public class CameraManager {
  public static CameraManager Instance;
  public EffectsController EffectsController=new();
  public Visor VisorEffect=new(); public Filter VisorSwitcher=new();
  public Vision NightVision=new(),ThermalVision=new(); public Camera Camera=new();
 }
 public class Camera { readonly Dictionary<Type,object> components=new() {{typeof(DeathFade),new DeathFade()},{typeof(FastBlur),new FastBlur()}}; public T GetComponent<T>() where T:class=>components[typeof(T)] as T; }
 public class Visor {public int Clears; public float Value; public void Clear(){Clears++;} public void SetIntensity(float value)=>Value=value;}
 public class Filter {Action<float> action; public void Init(Action<float> callback)=>action=callback; public void Set(float value)=>action(value);}
 public class Vision {public bool On=true; public void Switch(bool value)=>On=value;}
}
public class DeathFade {public bool Dead=true; public void DisableEffect()=>Dead=false;}
public class FastBlur {public bool Dead=true; public void Reset()=>Dead=false;}
public class EffectsController {
 public class EffectAccumulator {readonly EffectsController owner; public EffectAccumulator(EffectsController controller){owner=controller;} public List<object> ActiveEffects=new(); public int Removed; public void DeleteEffect(object effect){if(owner.Bound==null)throw new InvalidOperationException("Accumulator needs bound player while clearing");ActiveEffects.Remove(effect);Removed++;}}
 // These intentionally stay private: production accesses the installed fields through reflection.
 private EFT.Player player;
 private List<EffectAccumulator> _effectAccumulators; public EffectsController(){_effectAccumulators=new(){new(this),new(this)};}
 public IReadOnlyList<EffectAccumulator> Registry=>_effectAccumulators;
 public bool enabled=true; public int DamageCalls;
 public void Bind(EFT.Player target){player=target;target.HealthController.EffectStartedEvent+=OnEffectStarted;target.HealthController.EffectRemovedEvent+=OnEffectRemoved;target.OnDamageReceived+=OnPlayerDamaged;target.HealthController.ApplyDamageEvent+=OnHealthApplyDamage;target.HealthController.DiedEvent+=OnHealthDied;target.HealthController.BurnEyesEvent+=OnHealthBurnEyes;target.HealthController.StimulatorBuffEvent+=OnStimulatorBuff;target.OnGlassesChanged+=OnGlassesChanged;}
 public EFT.Player Bound=>player;
 public void OnEffectStarted(){} public void OnEffectRemoved(){} public void OnPlayerDamaged(){DamageCalls++;} public void OnHealthApplyDamage(){} public void OnHealthDied(){} public void OnHealthBurnEyes(){} public void OnStimulatorBuff(){} public void OnGlassesChanged(){}
 public void OnGlassesChanged(object unused,bool active){}
}

