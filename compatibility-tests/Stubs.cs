using System;
using System.Collections.Generic;
using System.Reflection;
namespace EFT { public class Player {} }
namespace UnityEngine
{
 public class Object
 {
  public static readonly List<Object> Instances=new();
  public Object(){Instances.Add(this);}
  public static T[] FindObjectsOfType<T>(bool inactive) where T:Object => Instances.FindAll(x=>x is T).ConvertAll(x=>(T)x).ToArray();
 }
 public class GameObject { public bool activeSelf=true; public void SetActive(bool value)=>activeSelf=value; }
 public class Component:Object { public GameObject gameObject=new(); }
}
namespace Fika.Core.Main.Components
{
 public class FikaHealthBar:UnityEngine.Component
 {
  public UnityEngine.Component _playerPlate=new();
  public static bool UseNamePlates=true;
  public int Toggles;
  public void Update(){}
  public void ToggleNamePlate(){Toggles++;_playerPlate.gameObject.SetActive(UseNamePlates);}
 }
}
namespace HarmonyLib
{
 public class HarmonyMethod { public HarmonyMethod(Type type,string method){} }
 public class Harmony
 {
  public int Patches;
  public void Patch(MethodBase target,HarmonyMethod prefix=null,HarmonyMethod postfix=null)
  {if(target==null)throw new Exception("Missing target");Patches++;}
 }
 public static class AccessTools
 {
  public static bool MapsAvailable;
  const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
  public static FieldInfo Field(Type type,string name)=>type?.GetField(name,Flags);
  public static PropertyInfo Property(Type type,string name)=>type?.GetProperty(name,Flags);
  public static MethodInfo Method(Type type,string name)=>type?.GetMethod(name,Flags);
  public static bool FovAvailable;
  public static Type TypeByName(string name)=>(name.StartsWith("FOVFix.")?FovAvailable:MapsAvailable)?typeof(AccessTools).Assembly.GetType(name):null;
 }
}
namespace FOVFix
{
 public class FovController { public EFT.Player _player; public void ControllerUpdate(){} }
}
namespace DynamicMaps
{
 public class Plugin { public static Plugin Instance=new(); public UI.ModdedMapScreen Map=new(); }
}
namespace DynamicMaps.UI
{
 public class ModdedMapScreen
 {
  public Dictionary<Type,object> _dynamicMarkerProviders=new();
  public bool _isShown=true;
  public int Shows;
  public void Show(bool animate)=>Shows++;
 }
}
namespace DynamicMaps.UI.Components
{
 public class PlayerMapMarker:UnityEngine.Component { public EFT.Player Player {get;private set;} public void Set(EFT.Player player)=>Player=player; }
}
namespace DynamicMaps.DynamicMarkers
{
 public class PlayerMarkerProvider { public UI.Components.PlayerMapMarker _playerMarker; public void TryAddMarker(){} }
}
namespace SptDiagnostics.Possession
{
 public class Plugin
 {
  public static Plugin Instance=new();
  public readonly List<Exception> Errors=new();
  public void Info(string value){}
  public void Error(Exception e)=>Errors.Add(e);
  public void Guard(Action action){try{action();}catch(Exception e){Error(e);}}
 }
}
