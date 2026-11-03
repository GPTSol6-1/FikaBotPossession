# Fika Bot Possession 1.0.0

Take over a bot after death or extraction in a private Fika raid. This release replaces the 0.1.0 remote-input prototype with a temporary locally owned Fika player and EFT's native input, first-person camera, weapon controllers, HUD and raid inventory screen.

**First public release, based on the tested 0.2.5 code.** The user reports a successful clean SPT/Fika test with only required dependencies and confirmed proper corpse creation on death in 0.2.5. Corpse looting was not separately reported. No takeover lockout occurred while at least one player remained alive, including in a possessed body; takeover after everyone enters freecam remains unverified. The user previously confirmed 0.2.4 bleed drain, bleeding treatment, missing-HP healing, painkillers and broken-leg running. Inconsistent AI targeting remains unresolved. This version prevents temporary characters from qualifying for Fika's earlier revival gate and logs takeover eligibility, host rejection and AI-target registration.

## Compatibility and installation

- Built against SPT 4.1.6 / EFT 0.16.9.5.40743 and Fika.Core 2.4.6.0. Other Fika assembly versions fail closed before patching.
- Requires the dedicated Fika headless raid host. A human-hosted raid is not supported by this version.
- Install the exact same DLL on **every participating human client and the headless game client**. No SPT backend mod is required.
- Close the affected game processes, back up the old DLL, and extract `BepInEx/plugins/FikaBotPossession/SPT.Diagnostics.FikaBotPossession.dll` into each game root. Restart those game processes. Do not leave a second copy of the old DLL elsewhere in `BepInEx/plugins`.
- LiveRaid Online and SAIN are optional. Their presence does not establish compatibility; test with the installed versions.

The 0.2.0 protocol is incompatible with 0.1.0. All clients must acknowledge preparation before control starts. A preparation failure or timeout restores the original bot and its AI. The host rejects concurrent claims on the same bot.

## Use

1. Keep another human alive so the raid continues. After dying or extracting, enter Fika's spectator/free camera.
2. Center a living humanoid bot holding a firearm and press **F6** (configurable in BepInEx).
3. Wait for the handoff. The original bot is replaced by a temporary character with its equipment, current stored health, ammunition, position, rotation and initial stance. This is a new network entity, preventing delayed packets for the old bot from controlling it.
4. Use your **normal Tarkov keybinds**. The usual inventory key opens the native equipment grids, item actions and search/loot interface. There is no custom item-list inventory or custom WASD/mouse relay.

An idle hint in the top left shows the configured takeover key while eligible in freecam. Takeover retains the existing raid camera and its graphics components. Fika map culling is temporarily disabled if needed, then its previous state is restored on return. This can increase rendering cost during possession; manual culling changes should not be necessary.

Start a **fresh raid** with the same version on all participants and possession enabled before entering spectator mode. This version preserves effects that Fika would normally destroy; it cannot recover components already destroyed in an earlier session or before enabling the plugin.

Fika's spectator extraction shortcut (normally F8) and its prompt are disabled during possession. To leave the raid, first use Leave in the temporary character's raid menu to end possession, then use spectator extraction after returning to freecam. Ending possession this way despawns the temporary body and its items. The shortcut does not extract or save the temporary character's loot.

The actual local player owns movement and combat simulation. The host validates native inventory operations through Fika. Network or host stalls can still delay remote observations, damage and inventory acknowledgements.

## Lifecycle and limitations

- **One-way handoff:** returning a living character to AI is not implemented. The old Delete-to-return shortcut is removed.
- Normal death is intended to retain a synchronized lootable corpse and return the controller to its original spectator context. Temporary bodies do not use Fika's human downed/revive system.
- Choosing Leave in the temporary character's raid menu ends possession and **despawns that body and its carried items**. Controller disconnect, a missing heartbeat for 12 seconds, disabling possession or raid teardown also retires the temporary body. It is not restored as an AI bot. Living-body handback is not implemented.
- Focus loss stops movement/fire input and mutes native input; it does not give the character back to AI. Heartbeats continue while connected.
- Simplified skeletons, stationary sniper bots, bots not holding a firearm and bots in stationary weapons or vehicles cannot be selected.
- The temporary profile is serialized separately from the original human. `CoopGame.LocalPlayer`, its original `PlayerOwner` and `CoopHandler.MyPlayer` remain assigned to the original human for raid saving. Camera/world presentation and client packet callbacks follow the temporary body only while possessed. Quest broadcasting from the temporary body is disabled; its quest/achievement data is cleared. No mechanism transfers its loot into the original human's inventory.
- Native initialization may interact with other mods that react to local-player creation, camera creation, inventory screens or death. These interactions, extraction/transit behavior and exact health-effect preservation remain unverified. Extraction of the temporary body into saved progression is not implemented.

## Acceptance test

Use disposable test profiles, a headless host and two human clients, with one human remaining alive. Preserve client/headless BepInEx logs and the server timeline.

1. Verify `1.0.0 loaded` on all three game processes. A failed reflection/Harmony contract must disable the plugin. Confirm the top-left takeover hint after extraction and after death.
2. Possess one ordinary scav. Confirm the host logs `preparing` then `committed`, and the controller logs `ClientInventoryController` and `Local control started`. A second human should see one body, not a frozen old bot plus a new character.
3. Compare walking, sprinting, crouching/prone, jumping, aiming, firing, recoil, optics and weapon changes with normal local player control. Confirm the observer sees the same actions and targets receive damage once.
4. Open native inventory. Move and rotate an item, equip a weapon, reload, unpack/repack ammunition, use medicine, drop and pick up an item, search a corpse/container and loot it. Compare item IDs/locations and counts on host/controller/observer. Stop on duplicated items or divergent inventories.
5. Cause an ordinary in-game death. Check that exactly one corpse remains, its equipment can be looted, and the controller regains spectator controls. Repeat possession of another bot.
   For the controller-side corpse issue, confirm the body has its head and normal third-person appearance, falls/settles, and can be looted after you possess another bot. Compare the exact corpse and item state with another human. Check `Local corpse created` and `Local corpse retained` report `exists=True`, `ragdoll=True`, and `body view=ThirdPerson`. These are construction diagnostics, not proof of a successful loot interaction.
   Leave spectator culling at its default: check distant scenery and interiors in possession, verify Amand's Graphics remains visible, and check that freecam moves after death without a black screen. The restoration log should report `camera retained=True, freecam=True`. Possess a second bot in the same raid and check that no `HysteresisFilter.Set` or `BotOwner.Position` exception appears. Repeat after manually disabling culling before takeover; that existing setting should remain in effect after returning. Check visor/night vision, damage effects and positional audio too.
   For 0.2.3, confirm `Health simulation started ... True` for each body. Obtain a bleed and verify HP falls over time without further shots. Bandage/tourniquet the appropriate bleed and use a medkit on a damaged limb: verify treatment completes, resources decrease and observers agree on the health result. Confirm the F8 prompt is hidden and the configured spectator exit key cannot stop the raid while possessing. End possession through Leave, check the prompt returns, and then exit from spectator mode. Check there are no radial-blur cleanup or bleeding-decal stale-player exceptions.
6. Attempt simultaneous claims. Exercise failed preparation, lost focus, controller disconnect, Leave and raid shutdown. Verify the documented restoration/despawn behavior. Do not simulate a missing plugin by changing installations mid-raid.
7. Repeat after extraction. Compare the original human's raid result and saved inventory with the expected outcome; temporary loot, kills and quests must not be saved to that profile. Test special extracts/transit separately before broader use.

## Development

Current code is in `v2/`. See BUILD.md for portable build instructions.

```powershell
dotnet build FikaBotPossession.csproj -c Release
dotnet run --project tests -c Release
dotnet run --project presentation-tests -c Release
./validate-contract.ps1
```

`GameRoot` defaults to the adjacent `client` game installation. The tests exercise the handoff gate and wire protocol without running Unity. The contract script checks metadata, not live Harmony behavior. See `VALIDATION.md` for validation results.

MIT licensed; see LICENSE. Original plugin code developed with OpenAI Codex assistance. Credits: SPT, Fika, BepInEx and Harmony. Requires the user's own EFT/SPT/Fika installation. The distribution contains no game binaries, Fika dependencies or decompiled source.

## Uninstall and support

Close all affected game processes, remove only this plugin's DLL from every human client and the headless client, then restart. Keep the same plugin version on all participants. For support include the version, steps to reproduce, and relevant BepInEx/LogOutput.log excerpts from the controller and headless host; remove sensitive profile or connection data before sharing.
