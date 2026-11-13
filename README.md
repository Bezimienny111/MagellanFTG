# Magellan FTG

A For the Glory adaptation of [Inferis/Magellan](https://github.com/Inferis/Magellan), with restored build dependencies and native FTG province data support.

This is a preview release of the map tools. The graphical application is a map viewer; a click-to-edit scenario editor is planned separately.

## FTG support

- Read and write native `Db/Map/provinces.txt`, retaining comments, unknown fields, original encoding and unchanged source fragments.
- Keep province capacity and the highest defined province ID separately for each map project.
- Support province IDs above the original Magellan limit, including full 16-bit IDs in memory and the FTG16 PSD ID converter.
- Rebase compressed lightmap owner tables when the ID range grows, preserving the original trees and shading where direct conversion applies.
- Handle FTG river references relative to the river section of the adjacency list.
- Export FTG map tables, `provinces.txt` and the matching `_MAP_MAX_PROVINCES_` setting in `Db/defines.txt`.
- Restore the missing EU2Edit, map controls and compression sources needed to build the tools; replace Explorer vendor controls with standard WinForms.

FTG still has an engine limit of **10000 province slots** (usable IDs below the configured capacity). The local ID-grid limit also remains **255 non-TI IDs per 2048 x 2048 region**. This project does not promise unlimited provinces or patch FTG.exe.

## Download and run

Download the Windows tools ZIP from [Releases](https://github.com/Bezimienny111/MagellanFTG/releases), extract it and keep all EXE and DLL files together. Windows and .NET Framework 4.8 are required. No installer is needed.

The package contains the ten original tool entry points: `mextract`, `minject`, `mimage`, `mprovince`, `mview`, `mcolor`, `mstats`, `mexport`, `MapExplorer` and `MapTest`. Use `/?` for command-line usage where supported.

Examples from the extracted tools directory, in PowerShell:

```powershell
# Read a mod's map into an editable Magellan project.
.\mextract.exe "MyMap.eu2map" /D:"C:\Games\For The Glory\Mods\MyMod" /3

# Open the map viewer.
.\mview.exe "MyMap.eu2map"

# Export native FTG province text, then import edited text into the project.
.\mprovince.exe /E "MyMap.eu2map" "provinces.txt" /TXT /O
.\mprovince.exe /I "provinces.txt" "MyMap.eu2map" /TXT

# Show the FTG16 ID color for province 9999.
.\mcolor.exe /ID:9999 /C:FTG16

# Write native files to a separate mod copy. This changes that destination.
.\minject.exe "MyMap.eu2map" /D:"C:\Games\For The Glory\Mods\MyMod_Copy"
```

Use a mod copy for injection. Reading can fall back to files in the parent FTG installation; output is written to the selected destination. Map shapes are edited through the layered PSD workflow using `mimage`, or through the map library; `mview` does not provide a painting interface.

## Build from source

On Windows with the .NET Framework 4.8 development/reference assemblies available:

```powershell
.\build.ps1 -Configuration Release -Rebuild
```

The script builds the production solution and collects the tools in `artifacts\bin\Release`. It uses the framework MSBuild installation; all restored project dependencies are included in this repository.

Optional integration checks require a local, legally installed Gloriana_WatK4 fixture, which is not distributed here:

```powershell
$env:MAGELLAN_FTG_MOD = 'C:\Games\For The Glory\Mods\Gloriana_WatK4'
.\build.ps1 -Configuration Release -Test
```

## Verification and current limits

Earlier integration runs passed 20 checks, including native text preservation, extraction, three-lightmap migration, high-ID native/PSD roundtrips and CLI workflows. Those checks are not a claim of complete engine compatibility at 10000 provinces.

On 13 November 2026 the user reported loading an FTG test scenario containing 144 added 16 x 16 provinces, IDs 2020-2163, and seeing the new island in game. A previous crash while starting that scenario remains unexplained. This release is a preview; the full province range has not been validated in game.

The scenario editor plan is saved in [SCENARIO_EDITOR_PLAN.md](SCENARIO_EDITOR_PLAN.md). It is a roadmap, not an implemented feature.

## Upstream and restored sources

Upstream base: Inferis/Magellan commit `f0abbda480906658fad0073d337c2dfe8dea5422`. Original history and source notices are retained.

Some missing libraries were restored from an existing local tree identifying itself as decompiled Magellan 2.4.0 and adapted to the upstream core. This does not establish binary equivalence to the original distribution. See [restoration-manifest.json](restoration-manifest.json) for provenance and source hashes, and [ftg-codec-evidence.json](ftg-codec-evidence.json) for the static codec findings.

The tools ZIP contains rebuilt tools, runtime dependencies and documentation. It does not contain FTG.exe, game map files, the Gloriana mod or a game installer. Some application version strings retain the upstream Magellan version; the GitHub release tag identifies the FTG fork release.
