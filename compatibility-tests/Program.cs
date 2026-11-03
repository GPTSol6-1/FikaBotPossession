using System;
using System.Reflection;
using EFT;
using Fika.Core.Main.Components;
using HarmonyLib;
using SptDiagnostics.Possession;
using DynamicMaps.DynamicMarkers;
using DynamicMaps.UI.Components;

static class Tests
{
 static int checks;
 static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
 static object Call(string name,params object[] args)=>typeof(PossessionCompatibility).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
 static void Main()
 {
  var hooks=new Harmony();
  PossessionCompatibility.Install(hooks);
  Check(hooks.Patches==2,"Dynamic Maps absent: Fika hooks still installed");
  AccessTools.MapsAvailable=true;
  PossessionCompatibility.Install(hooks);
  Check(hooks.Patches==5,"Dynamic Maps optional hook installed");
  var human=new Player();var bot=new Player();var other=new Player();
  AccessTools.FovAvailable=true;
  PossessionCompatibility.Install(hooks);
  var fov=new FOVFix.FovController{_player=human};
  Call("FovUpdate",fov);
  Check(fov._player==human,"FOV Fix unaffected outside possession");
  var self=new PlayerMapMarker();self.Set(human);
  var teammate=new PlayerMapMarker();teammate.Set(other);
  var provider=new PlayerMarkerProvider{_playerMarker=self};
  DynamicMaps.Plugin.Instance.Map._dynamicMarkerProviders.Add(typeof(PlayerMarkerProvider),provider);
  var bar=new FikaHealthBar();
  PossessionCompatibility.Begin(bot,human);
  Call("FovUpdate",fov);
  Check(fov._player==bot,"FOV Fix aim/optic watchers follow possessed body");
  Call("FovUpdate",fov);
  Check(!bar._playerPlate.gameObject.activeSelf,"existing name and health plate hidden immediately");
  Check(self.Player==bot,"cached main marker follows temporary body");
  Check(DynamicMaps.Plugin.Instance.Map.Shows==1,"visible map refreshes through native lifecycle");
  Check(teammate.Player==other,"other markers unchanged");
  Check((bool)Call("PlateUpdate",bar)==false,"native update cannot reactivate hidden plate");
  bar.ToggleNamePlate();Call("PlateToggled",bar);
  Check(!bar._playerPlate.gameObject.activeSelf,"config callback cannot reveal plate");
  var joining=new FikaHealthBar();Call("PlateUpdate",joining);
  Check(!joining._playerPlate.gameObject.activeSelf,"late player plate hidden");
  var replacement=new PlayerMapMarker();replacement.Set(bot);provider._playerMarker=replacement;
  Call("MarkerAdded",provider);
  FikaHealthBar.UseNamePlates=false;
  PossessionCompatibility.Restore();
  Check(fov._player==human,"FOV Fix original binding restored after repeated updates");
  Check(self.Player==human && replacement.Player==human,"all tracked self markers restored before temporary body removal");
  Check(!bar._playerPlate.gameObject.activeSelf && !joining._playerPlate.gameObject.activeSelf,"restore respects current disabled nameplate setting");
  Check(bar.Toggles==2 && joining.Toggles==1,"native plate configuration restored");
  Check((bool)Call("PlateUpdate",bar),"native updates resume in freecam");
  FikaHealthBar.UseNamePlates=true;
  for(int i=0;i<5;i++)
  {
   var next=new Player();
   PossessionCompatibility.Begin(next,human);
   Call("FovUpdate",fov);
   Check(fov._player==next,"FOV Fix follows each new possession");
   Check(!bar._playerPlate.gameObject.activeSelf,"repeat possession hides plates");
   PossessionCompatibility.Restore();
   Check(fov._player==human,"FOV Fix restores each return");
   Check(bar._playerPlate.gameObject.activeSelf,"repeat return restores configured plates");
   Check(replacement.Player==human,"repeat return restores marker");
  }
  PossessionCompatibility.Restore();
  DynamicMaps.Plugin.Instance.Map._isShown=false;
  int shows=DynamicMaps.Plugin.Instance.Map.Shows;
  PossessionCompatibility.Begin(bot,human);
  Check(DynamicMaps.Plugin.Instance.Map.Shows==shows,"hidden map stays hidden");
  PossessionCompatibility.Restore();
  Check(SptDiagnostics.Possession.Plugin.Instance.Errors.Count==0,"no guarded compatibility errors");
  Console.WriteLine($"PASS: {checks} compatibility lifecycle assertions against doubles; no Unity/Harmony runtime validation.");
 }
}
