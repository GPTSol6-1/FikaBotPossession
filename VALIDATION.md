# 1.0.1 release validation — 2026-11-03

User tested the revised build and confirms F7 kills the possessed bot, preserves its corpse and allows looting. Scoped sensitivity is also confirmed working. Earlier user testing confirmed Dynamic Maps is fixed, freecam return works and another possession succeeds. These are user-reported runtime results, not independent observation of every scope or multiplayer/last-player scenario. Plate visibility was not separately reported.

Release checks: clean Release build; 26,303 protocol assertions; 357 presentation assertions; 43 compatibility lifecycle assertions; 63 installed contract checks; two compiled F7 dispatch/native-death checks. Test doubles and metadata do not execute Unity/Harmony. Remaining coverage includes every-player-freecam takeover, inconsistent AI targeting, peer/owner looting matrix, last living possessed body and broad scope/mod combinations.

The development checklist below is historical; its pending claims are superseded by the confirmed results above only for the reported scenarios.
# 1.0.1 development candidate — 2026-11-03

Build: Release, zero warnings/errors. Checks: protocol 26,303; presentation doubles 357; new compatibility doubles 30; installed metadata/native-path checks 63 (including optional Dynamic Maps). Compatibility tests cover absent Dynamic Maps, existing and late-created plates, settings callbacks, restoring current settings, cached/recreated self markers, repeated possessions and refreshing only visible maps. They do not run Unity or Harmony. No installed/configuration changes or process control. Source backup: backups/FikaBotPossession/source-1.0.0-before-exit-overlays-maps.

Fresh-raid test required on matching human/headless candidate versions:

1. Possess while another human remains alive. Verify their name, health bar and effect/limb plate details are absent from the controller's view, including a late join or settings change. Other clients should retain their own normal display.
2. Press F7 (or the configured shortcut). Verify a third-person ragdoll and inventory remain on owner, peers and headless, control returns to freecam, then possess another bot and loot the previous body. Have another human loot it too. Verify Fika's spectator-exit key can leave the raid. Repeat when the possessed body is the last living controlled character while the raid remains active. Check a second possession and ordinary death still restore controls and plates. Raid-menu Leave/disconnect remain separate despawn actions.
3. In Factory or another non-Customs map, open Dynamic Maps before possession. After possession, verify labels, extract markers, position, heading and floor selection. Press M, close M, and toggle minimap/peek: the selected map must remain correct, and no dropdown may remain floating. Repeat with the map initially hidden and with another possession. Existing manual map/artwork selection may reset to the native first matching map on visible-map refresh; check this behavior too.
4. Return to freecam by F7 and by ordinary death; check the marker is restored and there are no stale temporary-body Transform errors. Repeat without Dynamic Maps installed in a separate test environment. Check original human raid results are unchanged.
5. With FOV Fix and Change Mouse Sensitivity enabled, compare the same weapon/scope on a normal human and possessed body: hip fire, iron/red dot, low/high magnification, variable zoom and toggle zoom. Confirm expected scaling and return-to-freecam behavior across repeated possession, ordinary death and F7. Repeat without FOV Fix in a separate environment to check native sensitivity. Earlier candidate's Dynamic Maps correction was confirmed by user; F7 native death and FOV binding changes are unverified in game.

Missing Location is a concrete code defect consistent with the wrong map/selector. Cached marker binding is a separate integration gap. Marker/text disappearance and dropdown persistence are not yet independently reproduced or proven fixed. Alternative causes include Dynamic Maps UI lifecycle behavior or another UI mod; investigate the first failing UI transition if this candidate does not resolve them. Evidence: diagnostics/bot-possession-101-2026-11-03.md.

# 1.0.0 release preparation — 2026-11-03

User reports latest code passed on a clean SPT/Fika installation with only documented dependencies. This is user-reported runtime evidence for the pre-release code, not an independent runtime test of the 1.0.0 metadata rebuild. Known limitations below remain disclosed.

# 0.2.5 development candidate validation — 2026-11-03

## User runtime result — 2026-11-03

User tested 0.2.5 and confirms dead bodies are now properly created on death. This validates corpse creation in the reported test; corpse looting, lethal damage types and original-human revival were not separately reported. No lockout occurred while at least one player remained alive, including when possessing a bot. The all-dead/all-freecam case was not exercised, so that lockout remains unverified. No new AI-targeting result was supplied. Evidence for this update is the user's report; no fresh log correlation or runtime test was performed by the agent.

## Preparation checks and remaining tests

Release build: zero warnings/errors. Protocol tests: 26,303 assertions. Presentation doubles: 357 assertions. Installed contract: 51 checks, including CanBeDowned and the native Fika revival Kill prefix's use of that getter. These checks do not execute the Harmony hooks or simulate a Unity death. No installed files/configuration or process state changed.

0.2.4 runtime feedback confirms bleed drain/treatment, HP healing, painkillers and broken-leg running. Six handoffs in the latest matching client/headless logs ended as dead; the owner retained no local corpse for entities 16/18/22/33 and created third-person ragdolls for 32/35. The earlier report that 0.2.4 was untested is historical below.

0.2.5 patches CanBeDowned only for the currently tracked temporary local body. Fika's Kill prefix uses that getter to skip native Kill; blocking only TryProcessDownedState failed to prevent the skip. Corpse creation is now user-confirmed, but detailed coverage remains: a non-headshot/non-grenade lethal wound while another human remains alive and revival is enabled, headshot/grenade death, actual looting after a second takeover, and death after all original humans die. For those tests, compare owner creation/retention logs with exists=True/ragdoll=True and visible settled corpse. Original human revival must still work.

AI targeting and takeover after all humans enter freecam remain unconfirmed. For targeting, compare a known hostile PMC with a faction-friendly scav at the same distance; retain host errors as well as registration logs. For lockout, press takeover once after the last human dies, recording the displayed notice. The client key log distinguishes eligibility/freecam/pending gates; the host request/rejection log distinguishes raid state, identity authorization and candidate AI inactivity. Do not infer that a NonActive target caused the reported failure until captured at the failed request.

## Historical 0.2.4 preparation checks

- Release compilation: passed, zero warnings/errors against installed game/Fika dependencies.
- Existing transaction/protocol tests: 26,303 assertions passed.
- Presentation tests: 357 assertions passed using production Presentation.cs and test doubles. The accumulator double now requires a bound player during effect removal, matching the installed radial-blur accumulator's skill lookup. Running these tests against backed-up 0.2.2 Presentation.cs fails with Accumulator needs bound player while clearing; current source passes. This catches the actual cleanup-order regression that the earlier doubles missed.
- Installed contract: 46 member checks and three native AI-removal call references pass (49 total). New checks cover the extraction prompt, spectator quit handler, health Start/_started and bleeding-decal removal API.
- Native inspection: Fika CoopGame.Spawn calls ClientHealthController.Start. The inherited PlayerHealthController.ManualEffectUpdate is gated by _started. Temporary mid-raid creation skipped Spawn. Fika CoopHandler.ProcessQuitting handles the spectator key independently of FreeCameraController.
- Source baseline and file timestamps/hashes saved under backups/FikaBotPossession/source-0.2.2-before-health-exit-fix.

## Runtime evidence for 0.2.2

User confirmed possession, death/freecam return, repeated takeover and retained graphics. Local/headless logs agree on five committed temporary entities (15,24,27,31,33); four end with corpse=True. The last ends after the client logs F8 pressed, attempting to extract, raid save/network destruction, and an exception reactivating freecam during teardown. This supports a faulty exit/cleanup path; no native crash dump was examined, so a hard process-crash mechanism is not independently established.

Additional local errors: radial-blur effect cleanup reads player skills after Presentation cleared its player pointer; EffectsCommutator.UpdatePlayersBleedings repeatedly visits a destroyed player's Transform. The candidate fixes the cleanup order and unregisters retiring entities from bleeding-decal tracking. Timed health effects now start at activation; this is a high-confidence explanation for non-progressing bleed/healing effects, but not a runtime proof that every medical action works.

## Required runtime verification

Start a fresh raid with the same 0.2.4 DLL on headless and all human clients, possession enabled before spectating and another human alive. Confirm each activation logs Health simulation started ... True. Obtain a bleed: verify HP falls with no further hits, then bandage/tourniquet and verify the bleed stops. Use a medkit on a damaged limb and compare resource use, animation completion and health across owner/observer/host. Verify F8 prompt is hidden while possessing and the configured spectator-exit shortcut cannot stop the session. End possession via Leave (despawns body/items), confirm freecam and prompt return, then exit via the spectator key. Repeat death/second takeover and check graphics/NVG/audio. Check no radial-blur cleanup, stale bleeding-player Transform or teardown freecam exceptions appear. Validate original-profile persistence separately.

**Not performed:** Unity load, live health/healing test, F8/Leave/death/disconnect runtime test or multiplayer health comparison for 0.2.4. Automated checks do not establish those behaviors. No installed files, configuration or process state changed during preparation.

## Controller-side corpse addition

The user clarified that their own former body floated, lacked normal body presentation and could not be looted, while other clients saw/looted a corpse. This narrows the symptom to the owner client; prior peer-side lootability is not full corpse validation. Source inspection shows local temporary bodies remain first-person, native corpse creation reuses that body, and freecam restoration only restores the original human. 0.2.4 switches the temporary Player and PlayerBody to third-person rendering before native FikaPlayer.CreateCorpse. It logs creation/retention, HasRagdoll and body view. It retains native corpse inventory/synchronization and does not synthesize another corpse.

**Still uncertain:** whether this render transition fully resolves floating/interaction behavior. No runtime measurements of the broken corpse's local collider or inventory registration were captured. The new diagnostics establish construction state; they cannot prove lootability. Retest by dying in a possessed body, possessing another bot and looting the prior corpse on the owner client while another human compares items and body appearance. Check exists=True/ragdoll=True/body view=ThirdPerson and verify actual item transfers.

0.2.4 Release build zero warnings/errors, existing 26,303 protocol and 357 presentation-double assertions pass; 46 member plus three native-call checks pass. Those tests do not execute corpse rendering/physics/loot interaction. No installation or process changes were made. Source baseline preserved at backups/FikaBotPossession/source-0.2.3-before-local-corpse-view.



