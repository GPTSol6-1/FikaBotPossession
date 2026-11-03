using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using Fika.Core.Main.Components;
using HarmonyLib;
using UnityEngine;

namespace SptDiagnostics.Possession;

// Presentation only: never replace Coop.MyPlayer, the human roster or raid-saving
// identity to make another mod follow a temporary character.
internal static class PossessionCompatibility
{
    static Player controlled,original;
    static readonly HashSet<FikaHealthBar> hiddenPlates=new();
    static readonly HashSet<Component> markers=new();
    static readonly FieldInfo plate=AccessTools.Field(typeof(FikaHealthBar),"_playerPlate");
    static readonly MethodInfo togglePlate=AccessTools.Method(typeof(FikaHealthBar),"ToggleNamePlate");
    static FieldInfo mapInstance,mapField,providers,playerMarker,mapShown;
    static MethodInfo showMap;
    static PropertyInfo markerPlayer;
    static FieldInfo fovPlayer;
    static readonly Dictionary<object,Player> fovPlayers=new();

    internal static void Install(Harmony h)
    {
        var fov=AccessTools.TypeByName("FOVFix.FovController");
        if(fov!=null)
        {
            try
            {
                fovPlayer=AccessTools.Field(fov,"_player");
                var update=AccessTools.Method(fov,"ControllerUpdate");
                if(fovPlayer?.FieldType!=typeof(Player) || update==null)
                    throw new MissingMemberException("FOV Fix player binding contract changed");
                h.Patch(update,prefix:new HarmonyMethod(typeof(PossessionCompatibility),nameof(FovUpdate)));
                Plugin.Instance.Info("FOV Fix possession player binding enabled.");
            }
            catch(Exception e) { fovPlayer=null; Plugin.Instance.Error(e); }
        }
        if(plate==null || togglePlate==null) throw new MissingMemberException("Fika nameplate contract changed");
        h.Patch(AccessTools.Method(typeof(FikaHealthBar),"Update"),
            prefix:new HarmonyMethod(typeof(PossessionCompatibility),nameof(PlateUpdate)));
        h.Patch(togglePlate,postfix:new HarmonyMethod(typeof(PossessionCompatibility),nameof(PlateToggled)));
        var plugin=AccessTools.TypeByName("DynamicMaps.Plugin");
        if(plugin==null) return;
        // Optional integration failure must not disable possession itself.
        try
        {
            var provider=AccessTools.TypeByName("DynamicMaps.DynamicMarkers.PlayerMarkerProvider");
            var screen=AccessTools.TypeByName("DynamicMaps.UI.ModdedMapScreen");
            var marker=AccessTools.TypeByName("DynamicMaps.UI.Components.PlayerMapMarker");
            mapInstance=AccessTools.Field(plugin,"Instance"); mapField=AccessTools.Field(plugin,"Map");
            providers=AccessTools.Field(screen,"_dynamicMarkerProviders");
            mapShown=AccessTools.Field(screen,"_isShown"); showMap=AccessTools.Method(screen,"Show");
            playerMarker=AccessTools.Field(provider,"_playerMarker");
            markerPlayer=AccessTools.Property(marker,"Player");
            var add=AccessTools.Method(provider,"TryAddMarker");
            if(mapInstance==null || mapField==null || providers==null || playerMarker==null || mapShown==null || showMap==null
                || markerPlayer?.GetSetMethod(true)==null || add==null)
                throw new MissingMemberException("Dynamic Maps player-marker contract changed");
            h.Patch(add,postfix:new HarmonyMethod(typeof(PossessionCompatibility),nameof(MarkerAdded)));
            Plugin.Instance.Info("Dynamic Maps player-marker integration enabled.");
        }
        catch(Exception e)
        {
            playerMarker=null;
            Plugin.Instance.Info("Dynamic Maps integration unavailable; possession remains enabled.");
            Plugin.Instance.Error(e);
        }
    }
    static void FovUpdate(object __instance)
    {
        if(controlled==null || fovPlayer==null) return;
        Plugin.Instance.Guard(()=>
        {
            if(!fovPlayers.ContainsKey(__instance)) fovPlayers.Add(__instance,(Player)fovPlayer.GetValue(__instance));
            fovPlayer.SetValue(__instance,controlled);
        });
    }
    internal static void Begin(Player player,Player human)
    {
        controlled=player; original=human;
        foreach(var bar in UnityEngine.Object.FindObjectsOfType<FikaHealthBar>(true)) Hide(bar);
        if(playerMarker==null) return;
        var plugin=mapInstance.GetValue(null);
        var map=plugin==null ? null : mapField.GetValue(plugin);
        if(map==null || providers.GetValue(map) is not IDictionary list) return;
        // Rebuild the currently visible map through its own lifecycle, now that
        // MainPlayer and Location are valid. Do not open a hidden map.
        if((bool)mapShown.GetValue(map)) showMap.Invoke(map,new object[]{false});
        foreach(var provider in list.Values)
            if(playerMarker.DeclaringType.IsInstanceOfType(provider)) BindMarker(provider);
    }
    static bool PlateUpdate(FikaHealthBar __instance)
    {
        if(controlled==null) return true;
        Hide(__instance); return false;
    }
    static void PlateToggled(FikaHealthBar __instance)
    {
        if(controlled!=null) Hide(__instance);
    }
    static void Hide(FikaHealthBar bar)
    {
        if(bar==null) return;
        if(plate.GetValue(bar) is Component ui && ui!=null)
        {
            hiddenPlates.Add(bar);
            ui.gameObject.SetActive(false);
        }
    }
    static void MarkerAdded(object __instance)
    {
        if(controlled!=null && playerMarker!=null) Plugin.Instance.Guard(()=>BindMarker(__instance));
    }
    static void BindMarker(object provider)
    {
        if(playerMarker.GetValue(provider) is not Component marker || marker==null) return;
        // Track only the provider's own main-player marker, never teammate/bot markers.
        markers.Add(marker);
        markerPlayer.SetValue(marker,controlled);
    }
    internal static void Restore()
    {
        controlled=null; // allow native ToggleNamePlate to apply current user settings
        foreach(var entry in fovPlayers)
            Plugin.Instance.Guard(()=>fovPlayer.SetValue(entry.Key,entry.Value));
        fovPlayers.Clear();
        foreach(var bar in hiddenPlates)
            if(bar!=null) Plugin.Instance.Guard(()=>togglePlate.Invoke(bar,null));
        hiddenPlates.Clear();
        foreach(var marker in markers)
            if(marker!=null) Plugin.Instance.Guard(()=>markerPlayer.SetValue(marker,original));
        markers.Clear(); original=null;
    }
}
