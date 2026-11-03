param([string]$GameRoot = (Join-Path $PSScriptRoot '..\client'))
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $GameRoot 'BepInEx\core\Mono.Cecil.dll')
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameRoot 'EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll'))
$fika = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameRoot 'BepInEx\plugins\Fika\Fika.Core.dll'))
$checks = 0
function Require-Member($Assembly, [string]$Type, [string]$Member, [string]$Kind = 'Method') {
    $definition = $Assembly.MainModule.Types | Where-Object FullName -EQ $Type
    if ($null -eq $definition) { throw "Missing type $Type" }
    if ($Kind -eq 'Field') { $members = @($definition.Fields | Where-Object Name -EQ $Member) }
    else { $members = @($definition.Methods | Where-Object Name -EQ $Member) }
    if ($members.Count -ne 1) { throw "Expected exactly one $Kind $Type::$Member; found $($members.Count)" }
    $script:checks++
}
function Require-Call([string]$Type, [string]$Member, [string]$TargetType, [string]$TargetMember) {
    $definition = $game.MainModule.Types | Where-Object FullName -EQ $Type
    $method = $definition.Methods | Where-Object Name -EQ $Member
    $calls = @($method.Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq $TargetType -and $_.Operand.Name -eq $TargetMember
    })
    if ($calls.Count -eq 0) { throw "Native retirement path changed: $Type::$Member no longer calls $TargetType::$TargetMember" }
    $script:checks++
}
try {
    if ($fika.Name.Version.ToString() -ne '2.4.6.0') { throw 'Fika.Core must be 2.4.6.0' }
    foreach ($name in @('UpdateManual', 'FixedUpdate')) { Require-Member $game 'EFT.BotOwner' $name }
    foreach ($name in @('OnLoadingProfilePacketReceived')) { Require-Member $fika 'Fika.Core.Networking.FikaServer' $name }
    Require-Member $fika 'Fika.Core.Main.Players.ObservedPlayer' 'OnDead'
    Require-Member $fika 'Fika.Core.Networking.Packets.Generic.SubPackets.ClientExtractPacket' 'Execute'
    Require-Member $fika 'Fika.Core.Main.Players.FikaPlayer' 'LocalPlayerDiedAsync'
    Require-Member $fika 'Fika.Core.Main.Players.FikaPlayer' 'CreateCorpse'
    Require-Member $fika 'Fika.Core.Main.ClientClasses.ClientHealthController' 'TryProcessDownedState'
    Require-Member $fika 'Fika.Core.Main.ClientClasses.ClientHealthController' 'get_CanBeDowned'
    $killPatch = $fika.MainModule.Types | Where-Object FullName -EQ 'Fika.Core.Main.Patches.Revival.ClientHealthController_Kill_Patch'
    $killPrefix = $killPatch.Methods | Where-Object Name -EQ 'Prefix'
    if (-not ($killPrefix.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'get_CanBeDowned' })) {
        throw 'Fika revival Kill prefix no longer checks CanBeDowned; review temporary death integration'
    }
    $checks++
    Require-Member $fika 'Fika.Core.Main.Components.CoopHandler' 'SyncPlayersWithClients'
    Require-Member $game 'EFT.CameraControl.PlayerCameraController' 'OnDestroy'
    Require-Member $game 'EFT.CameraControl.CameraManager' 'SetCameraFromSettings'
    Require-Member $game 'EFT.CameraControl.CameraManager' '_occlusionCullingEnabled' 'Field'
    Require-Member $fika 'Fika.Core.Main.FreeCamera.FreeCameraController' '_hasEnabledCulling' 'Field'
    Require-Member $fika 'Fika.Core.Main.FreeCamera.FreeCameraController' '_extractText' 'Field'
    Require-Member $fika 'Fika.Core.Main.Components.CoopHandler' 'ProcessQuitting'
    Require-Member $game 'EFT.HealthSystem.PlayerHealthController' 'Start'
    Require-Member $game 'EFT.HealthSystem.PlayerHealthController' '_started' 'Field'
    Require-Member $game 'Systems.Effects.EffectsCommutator' 'StopBleedingForPlayer'
    Require-Member $fika 'Fika.Core.Main.FreeCamera.FreeCameraController' 'ClearEffects'
    Require-Member $game 'EffectsController' 'OnClientPlayerEffectsPauseEvent'
    foreach ($name in @('player','_effectAccumulators')) { Require-Member $game 'EffectsController' $name 'Field' }
    foreach ($name in @('action_2','action_3','action_4','action_5','action_6')) { Require-Member $game 'EFT.CameraControl.PlayerCameraController' $name 'Field' }
    Require-Member $game 'FastBlur' 'Reset'
    Require-Member $game 'EFT.BotsController' 'BotDied'
    Require-Member $game 'EFT.BotSpawner' 'BotDied'
    Require-Member $game 'BotsList' 'Remove'
    Require-Member $game 'BotsGroup' 'RemoveAlly'
    Require-Call 'EFT.BotsController' 'BotDied' 'EFT.BotSpawner' 'BotDied'
    Require-Call 'EFT.BotSpawner' 'BotDied' 'BotsList' 'Remove'
    Require-Call 'BotsList' 'Remove' 'BotsGroup' 'RemoveAlly'
    foreach ($name in @('GetSensitivity','GetAimingSensitivity','_vaultingComponent','_vaultingComponentDebug','_vaultingParameters','_vaultingGameplayRestrictions')) {
        Require-Member $game 'EFT.Player' $name 'Field'
    }
    foreach ($name in @('_insurance','_gameDateTime','_location','_myPlayer')) { Require-Member $game 'EFT.GamePlayerOwner' $name 'Field' }
    Require-Member $fika 'Fika.Core.Main.Players.ObservedPlayer' '_observedVaultingParameters' 'Field'
    Require-Member $fika 'Fika.Core.Main.ClientClasses.ClientQuestController' '_canSendAndReceive' 'Field'
    Require-Member $fika 'Fika.Core.Main.ClientClasses.ClientSharedQuestController' '_acceptedTypes' 'Field'
    Require-Member $game 'EFT.Player' 'InitVaultingAudioControllers'
    Require-Member $game 'EFT.Player' 'set_Location'
    foreach ($name in @('Update','ToggleNamePlate')) { Require-Member $fika 'Fika.Core.Main.Components.FikaHealthBar' $name }
    Require-Member $fika 'Fika.Core.Main.Components.FikaHealthBar' '_playerPlate' 'Field'
    $mapsPath=Join-Path $GameRoot 'BepInEx/plugins/mpstark-dynamicmaps/DynamicMaps.dll'
    if(Test-Path $mapsPath) {
        $maps=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($mapsPath)
        try {
            foreach($entry in @(
                @('DynamicMaps.Plugin','Instance','Field'), @('DynamicMaps.Plugin','Map','Field'),
                @('DynamicMaps.UI.ModdedMapScreen','_dynamicMarkerProviders','Field'),
                @('DynamicMaps.UI.ModdedMapScreen','_isShown','Field'),
                @('DynamicMaps.UI.ModdedMapScreen','Show','Method'),
                @('DynamicMaps.DynamicMarkers.PlayerMarkerProvider','_playerMarker','Field'),
                @('DynamicMaps.DynamicMarkers.PlayerMarkerProvider','TryAddMarker','Method'),
                @('DynamicMaps.UI.Components.PlayerMapMarker','set_Player','Method'))) {
                Require-Member $maps $entry[0] $entry[1] $entry[2]
            }
        } finally { $maps.Dispose() }
    }
    "PASS: $checks member/native-path checks; Fika.Core $($fika.Name.Version)."
    'This validates metadata and native call references, not Harmony installation or gameplay.'
}
finally { $game.Dispose(); $fika.Dispose() }
