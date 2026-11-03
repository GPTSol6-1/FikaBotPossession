# Fika Bot Possession 1.0.0

# One bad peek. A whole raid left to play.

Your PMC is down. Your friends are still fighting. Somewhere nearby, a scav is walking around with a perfectly usable body.

**Borrow it.**

Fika Bot Possession lets you jump out of spectator freecam and into a living bot. Find your next pair of boots, press **F6**, and get back into the raid with that bot's gear, weapons, ammunition and remaining health. Your usual Tarkov controls take over: aim, fire, reload, open your inventory, search containers and use medicine through the game's native interfaces.

No remote-control panel. No second set of movement keys. Just a different body and whatever it has in its pockets.

## Back in the fight

- **Pick your next perspective.** After death or extraction, aim your free camera at a living armed bot and request a takeover. A small on-screen hint shows your configured key.
- **Play the character you're given.** Its equipment and current stored health come with it. That battered scav kit is your new loadout.
- **Use familiar controls.** First-person weapons, HUD, inventory grids, item actions and looting use native Tarkov systems.
- **Keep the session going.** When a possessed body dies, return to freecam and look for another opportunity while the raid remains active.
- **Built for private Fika sessions.** The dedicated headless host coordinates each handoff and prevents two players from claiming the same bot.

## Get started

You need **SPT 4.1.6**, **Fika.Core 2.4.6.0**, and a **dedicated Fika headless raid host**. Human-hosted raids are not supported. The plugin checks the exact Fika assembly version before enabling its hooks.

1. Close the affected game clients and headless client. Back up any older possession DLL and remove duplicate copies.
2. Extract the archive's `BepInEx` folder into **every participating human client's game folder and the headless game folder**. All participants need the same plugin version. No additional SPT backend mod is required.
3. Start a fresh raid. After dying or extracting, enter Fika freecam, center a living humanoid bot holding a firearm, and press **F6**. Change the key in BepInEx settings if you prefer.

The installed file should be `BepInEx/plugins/FikaBotPossession/SPT.Diagnostics.FikaBotPossession.dll`.

Keep another player alive so the raid continues. Takeover after everyone is dead and in freecam remains unverified. LiveRaid Online and SAIN are optional; compatibility with every mod combination is not guaranteed.

## A second body, not a second extraction

Possession is for staying in the action. **It does not save the temporary character's loot, quests or progression to your original PMC.** Your original character keeps its own raid result.

This release uses a **one-way handoff**: a living possessed character cannot be handed back to AI. Normal death creates a corpse and returns you to freecam; temporary characters do not use Fika's human downed/revive system. Corpse creation is confirmed in user testing, while detailed controller-side corpse-looting coverage remains pending.

**Choosing Leave despawns the possessed body and everything it carries.** Disconnecting, losing the controller heartbeat, disabling possession or ending the raid also retires it. Fika's spectator extraction shortcut is blocked while possessing: use Leave to return to freecam first, then exit as a spectator.

Stationary snipers, simplified-skeleton bots, unarmed bots and bots using stationary weapons or vehicles cannot be selected. Fika map culling is temporarily disabled when necessary during possession and restored afterward; this can increase rendering cost.

**Known issues:** AI targeting can be inconsistent, and takeover after all players enter freecam remains unverified. Special extraction/transit behavior, every health-effect combination and interactions with other camera, inventory or death mods have not been exhaustively tested.

## Support and credits

If a handoff fails, include the plugin version, what happened, and relevant `BepInEx/LogOutput.log` excerpts from your client and the headless host. Redact private profile and connection data. To uninstall, close the games, remove this plugin's DLL from each client and the headless host, then restart.

[Source and build instructions](https://github.com/GPTSol6-1/FikaBotPossession) · **MIT license**

Built with SPT, Fika, BepInEx and Harmony. Code and documentation developed with OpenAI Codex assistance. Cover illustration generated with OpenAI's built-in image generation tool. The cover is promotional artwork, not a gameplay screenshot. No game or dependency assemblies are bundled.


## Build and validation

See BUILD.md for build instructions. This plugin targets SPT 4.1.6 / EFT 0.16.9.5.40743 and Fika.Core 2.4.6.0. Compiled source is in v2/.

The latest pre-release code passed clean SPT/Fika testing with required dependencies according to the user. The 1.0.0 build changes release metadata only. Release build passed with zero warnings/errors; existing checks passed 26,303 protocol assertions, 357 presentation-double assertions and 51 dependency-contract checks. Those checks do not simulate Unity gameplay. See VALIDATION.md for detailed test scope and remaining coverage.
