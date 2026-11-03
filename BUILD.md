# Build Fika Bot Possession 1.0.0

Use Windows, the .NET 10 SDK, and your own SPT 4.1.6 installation with Fika.Core 2.4.6.0 installed. Dependencies are referenced from the game and are not distributed here.

```powershell
dotnet build FikaBotPossession.csproj -c Release -p:GameRoot="E:/SPT/4.1.x"
dotnet run --project presentation-tests -c Release
./validate-contract.ps1 -GameRoot "E:/SPT/4.1.x"
```

The compiled plugin is `bin/Release/netstandard2.1/SPT.Diagnostics.FikaBotPossession.dll`. Install it in `BepInEx/plugins/FikaBotPossession/` on every human client and the dedicated headless game client. No backend plugin is included.

The protocol tests expect a game installation at `../client` relative to the source root. With that layout, run `dotnet run --project tests -c Release`. The main build also defaults to that game path if GameRoot is omitted. Presentation tests use doubles; protocol tests and metadata checks do not validate Unity/Harmony runtime behavior.

The compiled source is `v2/*.cs`. No game code or third-party assemblies are bundled. Original code and documentation are MIT licensed. SPT, Fika, BepInEx and Harmony retain their own licenses. Developed with OpenAI Codex assistance; no generated art or audio is included.
