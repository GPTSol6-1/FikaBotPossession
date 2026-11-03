# Changelog

## 1.0.1 — 2026-11-03

- Add a configurable End possession shortcut (F7 by default) and a persistent on-screen hint. F7 now kills the possessed character through native health/death handling, retaining the corpse and inventory before returning to freecam. This replaces the earlier candidate's despawn behavior, per user feedback. Raid-menu Leave and disconnect cleanup still retire the temporary body. Original-character persistence is unchanged.
- Add optional FOV Fix compatibility: bind its cached player to the possessed character while its controller updates, then restore the previous binding on return. Its ADS/optic watchers otherwise continue reading the original human, preventing the configured magnification sensitivity scaling. User confirmed scope sensitivity now works.
- Hide Fika player names, health bars and associated plate information on the controlling client during possession. Apply native nameplate settings again on return, including configuration changes made during possession.
- Assign the current raid Location to the temporary player, which bypasses normal raid spawning. Dynamic Maps reads this value when selecting maps; an unset value can expose unrelated maps and a map selector.
- Add optional Dynamic Maps compatibility: refresh an already visible map through its native lifecycle, rebind its cached main-player marker to the possessed body and restore it afterward. Hidden maps remain closed. Dynamic Maps is not a required dependency.
- User confirmed Dynamic Maps is fixed, F7 kills the possessed character and preserves a lootable corpse, and scope sensitivity scales correctly. Earlier testing also confirmed return to freecam and subsequent possession. This does not establish every scope, last-player or multiplayer-looting scenario.
- Release build, 26,303 protocol assertions, 357 presentation assertions, 43 compatibility lifecycle assertions with doubles and 63 installed dependency checks pass. These are development checks alongside the user-reported runtime results above.

## 1.0.0 — 2026-11-03 — first public release

- Release the locally owned bot takeover implementation developed and tested in 0.2.0–0.2.5: normal Tarkov controls, first-person camera, weapons, HUD, native inventory and looting.
- Include camera/effects retention, native AI retirement, timed health simulation, spectator-exit protection and the temporary-character revival gate correction. Corpse creation and health behavior were confirmed in user testing.
- Requires SPT 4.1.6, Fika.Core 2.4.6.0 and a dedicated headless host, with the same plugin on all human clients and the headless client.
- One-way possession: Leave/disconnect despawns the temporary body and items; possession does not save loot or progression to the original human.
- Known limitations: inconsistent AI targeting remains unresolved; takeover after all players enter freecam remains unverified. Detailed corpse-looting coverage remains pending.
- User reports successful clean SPT/Fika testing with only required dependencies. The 1.0.0 rebuild changes release metadata and documentation only; no new gameplay fix is claimed.
- Set assembly/plugin version to 1.0.0 and use the Forge author-prefixed plugin display name. Publish original source under MIT with OpenAI Codex assistance disclosed.

## 0.2.5 — 2026-11-03 — corpse creation verified by user

- Prevent temporary possessed characters from qualifying for Fika revival at the earlier CanBeDowned gate. Fika's Kill prefix can otherwise skip the native local death routine, even though the blocked downed-state transition sends a normal death packet to peers. User tested 0.2.5 and confirmed bodies are now properly created on death. Corpse looting was not separately reported in this test.
- No takeover lockout occurred in the user's 0.2.5 test while at least one player remained alive, including when possessing a bot. Takeover after every player is dead and in freecam remains unverified; this test does not establish that the lockout is fixed.
- Log takeover key eligibility/freecam state, host request/candidate AI state, rejection reasons and replacement AI-target registration to investigate the all-freecam lockout and inconsistent targeting. Neither issue is claimed fixed.
- User verified 0.2.4 bleed health drain, bleeding treatment, missing-HP healing, painkillers and running with a broken leg. These remain confirmed runtime results for 0.2.4.
- 0.2.4 corpse correction was insufficient: four of six logged deaths retained no local corpse; two did create third-person ragdolls.

## 0.2.4 — 2026-11-03 — controller-side corpse presentation candidate

- Switch temporary local characters and their PlayerBody to third-person rendering before Fika creates their corpse. Native corpse creation reuses the existing body; returning freecam previously restored only the original human's view, leaving the temporary body's first-person presentation behind.
- Log controller-side corpse creation/retention, ragdoll presence and body view to distinguish rendering problems from incomplete corpse creation.
- Preserve the native corpse and inventory flow used by peers. No duplicate corpse or inventory is synthesized.
- Includes all 0.2.3 health/exit corrections. The controller's reported floating/unlootable corpse still requires a runtime retest; successful peer looting did not validate the controller's view.

## 0.2.3 — 2026-11-03 — health and spectator-exit candidate

- Start the temporary ClientHealthController at activation, matching Fika's normal spawn lifecycle. Without Start, timed effects do not advance, affecting bleeds and medical effects despite direct damage working.
- Block Fika's spectator extraction shortcut while a possession handoff is active and hide its extraction prompt while controlling a body. Restore spectator exit after possession ends.
- Skip spectator UI reactivation during full raid/network teardown.
- Clear presentation effects before detaching their player reference; radial-blur cleanup reads that player's skills even when removing effects. Strengthen the regression test to enforce this dependency.
- Remove retiring entities from bleeding-decal tracking before destruction, addressing stale Transform errors in EffectsCommutator.
- User confirmed 0.2.2 possession, repeated death/freecam return and retained graphics. These additional 0.2.3 corrections require runtime verification.

## 0.2.2 — 2026-11-03 — lifecycle regression candidate

- Preserve visor, health-effect and blood-effect components during Fika spectator cleanup while clearing the old character's active effects. Keep the live camera and its graphics/freecam components.
- Detach health/damage/vision event handlers before binding a new character, initialize the visor callback before camera-controller construction, and reset death fade/blur on spectator return.
- Clean partially constructed camera controllers without calling missing unsubscribe delegates. Ignore player-specific effect-pause notifications while the retained effects controller has no player.
- Retire the old AI through native population and group removal before registering the replacement or destroying the old body. Previously DeletePlayer alone left its BotOwner in the group, breaking later handoffs.
- Attempt entity removal even if perception cleanup throws. Add presentation lifecycle tests with doubles and checks of the installed native AI removal call chain.
- Addresses the observed 0.2.1 activation and subsequent host-commit failures. Fresh-raid runtime validation remains pending.

## 0.2.1 — 2026-11-03 — regression-test candidate

- Add a persistent, readable top-left takeover-key hint while eligible in freecam.
- Retain the live raid camera during temporary player camera-controller construction. EFT's default construction destroys the camera that holds graphics effects and Fika's spectator script.
- Temporarily disable Fika map culling when needed and restore the prior setting on return; preserve Unity occlusion settings too. Rendering cost may increase during possession.
- Clear death fade and restore shared visor/audio bindings on spectator return. Add a camera/freecam restoration diagnostic and metadata checks for the new hook/private fields.
- First 0.2.0 user test confirmed weapon controls, native inventory/looting and a lootable corpse. These 0.2.1 corrections remain unverified in a raid.

## 0.2.0 — 2026-11-03 — experimental candidate

- Replace host-driven button commands with a host-approved, locally owned Fika player initialized from the selected bot's state.
- Use EFT's normal player input, camera, hands controllers, HUD and raid inventory interface; remove the custom inventory panel and hardcoded movement key mapping.
- Add owner/observer preparation acknowledgements, exclusive claims, inventory controller seed synchronization, rollback before commitment and generation-scoped terminal messages.
- Allocate a separate profile/network identity for the temporary character and retire the old bot after commitment. Preserve the original human's raid-saving objects.
- Add native spectator/camera/input restoration, temporary quest-broadcast isolation, death notification suppression for the original human backend identity, focus handling and controller heartbeats.
- Require a dedicated headless host. Living-body return to AI is deferred; intentional exit/disconnect retires the temporary entity and carried items. Normal death is intended to retain its corpse.
- Add standalone state-machine/protocol tests and metadata checks for reflection/Harmony targets. Gameplay validation remains pending.

## 0.1.0 — 2026-11-02

Initial experimental host-authoritative control relay, first-person presentation of observed bots and a basic host-backed item panel. The user confirmed bot takeover worked, but reported lag and an unusable inventory panel.
