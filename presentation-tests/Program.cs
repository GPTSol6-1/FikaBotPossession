using System;
using System.Linq;
using EFT;
using EFT.CameraControl;
using SptDiagnostics.Possession;
static class Tests {
 static int checks;
 static void Check(bool condition,string name){checks++;if(!condition)throw new Exception(name);}
 static void Main(){
  var manager=CameraManager.Instance=new CameraManager();
  var effects=manager.EffectsController; var registry=effects.Registry;
  var original=new Player(); effects.Bind(original);
  foreach(var accumulator in registry){accumulator.ActiveEffects.Add(new());accumulator.ActiveEffects.Add(new());}
  Presentation.Spectate();
  Check(original.Listeners==0,"spectator must detach all original health/damage/vision events");
  Check(ReferenceEquals(effects,manager.EffectsController)&&ReferenceEquals(registry,effects.Registry)&&registry.Count==2,"spectating must retain components and accumulator registry");
  Check(registry.All(x=>x.ActiveEffects.Count==0&&x.Removed==2),"remove old effects without losing accumulator definitions");
  Check(!effects.enabled&&!Presentation.HasPlayer(effects),"spectator effect controller inactive and unbound");
  Check(!manager.Camera.GetComponent<DeathFade>().Dead&&!manager.Camera.GetComponent<FastBlur>().Dead,"spectator resets death fade and blur");
  manager.VisorSwitcher.Set(.5f);Check(manager.VisorEffect.Value==.5f,"spectator visor callback remains valid");
  for(int i=0;i<50;i++){
   Presentation.Begin();Check(effects.enabled,"possession enables retained controller");
   manager.VisorSwitcher.Set(.75f);Check(manager.VisorEffect.Value==.75f,"visor initialization usable before camera construction");
   var body=new Player();effects.Bind(body);Check(body.Listeners==8&&Presentation.HasPlayer(effects),"one character owns eight presentation subscriptions");
   int before=effects.DamageCalls;original.Damage();body.Damage();Check(effects.DamageCalls==before+1,"old character cannot drive new presentation");
   Presentation.Begin();Check(body.Listeners==0,"rebind detaches previous character before replacing owner");effects.Bind(body);
   Presentation.Spectate();Presentation.Spectate();Check(body.Listeners==0&&effects.Bound==null,"repeat return does not leak subscriptions");
   Check(ReferenceEquals(registry,effects.Registry)&&registry.Count==2,"repeat handoffs preserve registry");
  }
  manager.VisorSwitcher=null;bool rejected=false;try{Presentation.Begin();}catch(InvalidOperationException){rejected=true;}Check(rejected,"missing spectator components fail with an actionable error");
  Console.WriteLine($"PASS: {checks} presentation assertions against test doubles; no Unity runtime validation.");
 }
}
