# Fika Bot Possession 1.0.1

# Stay in the raid with your friends.

**Multiplayer bot possession for Fika.** When your PMC dies or extracts, take control of a living bot and jump back into the same raid with the other players. Your friends keep playing, you get a new body, and the action carries on together.

Fika Bot Possession is built around a shared multiplayer raid: your possessed character moves, fights and interacts in the same world as everyone else. Other players see your new body and its actions through Fika's networking, while you use your normal Tarkov controls, first-person weapons, HUD, inventory and looting interface.

Get dropped early? Open spectator freecam, aim at a living armed bot and press **F6**. Its gear, ammunition and remaining health become your next loadout. A scav with a battered rifle can become your next chance to get back into the fight while your friends are still in the raid.

## Multiplayer comes first

- **Rejoin the ongoing action.** Take over after death or extraction without waiting for your friends to finish the raid.
- **Share the same fight.** Your controlled character exists in the multiplayer raid alongside the other human players.
- **Possess different bots.** The headless host coordinates handoffs and prevents players from claiming the same bot at once.
- **Use native Tarkov controls.** Aim, shoot, reload, manage equipment, use medicine and loot through the familiar interfaces.
- **Try another body.** On death, return to freecam and look for another opportunity while the raid remains active.

## Get started

You need **SPT 4.1.6**, **Fika.Core 2.4.6.0**, and a **dedicated Fika headless raid host**. Human-hosted raids are not supported. The plugin checks the exact Fika assembly version before enabling its hooks.

1. Close the affected game clients and headless client. Back up any older possession DLL and remove duplicate copies.
2. Extract the archive's `BepInEx` folder into **every participating human client's game folder and the headless game folder**. All participants need the same plugin version. No additional SPT backend mod is required.
3. Start a fresh raid. After dying or extracting, enter Fika freecam, center a living humanoid bot holding a firearm, and press **F6**. Change the key in BepInEx settings if you prefer.

The installed file should be `BepInEx/plugins/FikaBotPossession/SPT.Diagnostics.FikaBotPossession.dll`.

Keep another player alive so the raid continues. Takeover after everyone is dead and in freecam remains unverified. LiveRaid Online and SAIN are optional; compatibility with every mod combination is not guaranteed.

## A second body, not a second extraction

Possession is for staying in the action. **It does not save the temporary character's loot, quests or progression to your original PMC.** Your original character keeps its own raid result.

This release uses a **one-way handoff**: a living possessed character cannot be handed back to AI. Normal death creates a corpse and returns you to freecam; temporary characters do not use Fika's human downed/revive system. User testing confirms corpse creation and looting after F7.

**Press F7 to kill the possessed character and return to freecam, leaving its corpse and inventory for looting.** The shortcut is configurable under Controls -> End possession in BepInEx settings and remains visible on screen while controlling a body. Native death and Fika synchronization handle the corpse; user testing confirms F7 preserves a lootable body. The raid menu's Leave action requests retirement instead. Disconnecting, losing the controller heartbeat, disabling possession or ending the raid also retires the body. Fika's spectator extraction shortcut is blocked while possessing: end possession first, then exit as a spectator.

Optional FOV Fix support redirects its cached aim/optic player during possession and restores it afterward, so its configured scope sensitivity follows the controlled weapon. User testing confirms the scope sensitivity correction works. FOV Fix is not required.

During possession, Fika teammate names and health plates are hidden on your client; your configured display resumes on return to freecam. Dynamic Maps integration supplies the temporary body's raid location, refreshes an already visible map and redirects its main-player marker. User testing confirms the reported Dynamic Maps bugs are fixed.

Stationary snipers, simplified-skeleton bots, unarmed bots and bots using stationary weapons or vehicles cannot be selected. Fika map culling is temporarily disabled when necessary during possession and restored afterward; this can increase rendering cost.

**Known issues:** AI targeting can be inconsistent, and takeover after all players enter freecam remains unverified. Special extraction/transit behavior, every health-effect combination and interactions with other camera, inventory or death mods have not been exhaustively tested.

## Support and credits

If a handoff fails, include the plugin version, what happened, and relevant `BepInEx/LogOutput.log` excerpts from your client and the headless host. Redact private profile and connection data. To uninstall, close the games, remove this plugin's DLL from each client and the headless host, then restart.

[Source and build instructions](https://github.com/GPTSol6-1/FikaBotPossession) · **MIT license**

Built with SPT, Fika, BepInEx and Harmony. Code and documentation developed with OpenAI Codex assistance. Cover illustration generated with OpenAI's built-in image generation tool. The cover is promotional artwork, not a gameplay screenshot. No game or dependency assemblies are bundled.


## Build and validation

See BUILD.md for build instructions. This plugin targets SPT 4.1.6 / EFT 0.16.9.5.40743 and Fika.Core 2.4.6.0. Compiled source is in v2/.

The published 1.0.0 behavior passed clean SPT/Fika testing with required dependencies according to the user. The 1.0.1 candidate builds with zero warnings/errors and passes 26,303 protocol assertions, 357 presentation-double assertions, 30 compatibility-double assertions and 63 dependency-contract checks with the installed optional Dynamic Maps assembly. These checks do not simulate Unity gameplay. See VALIDATION.md for the new runtime test sequence and historical coverage.

