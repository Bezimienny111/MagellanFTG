# FTG scenario editor development plan

Agreed on 13 November 2026. Status: saved for future implementation; development of the scenario editor has not started.

Build a graphical scenario editor on the restored Magellan map libraries. A user selects an FTG mod and scenario, clicks a province on the map, edits its starting conditions in a side panel, reviews the changes and saves them to the appropriate scenario files.

## Existing foundation

- `Maptools/MapView/MainForm.cs` already resolves cursor coordinates to a province through `file.IDMap[e.X, e.Y]` in `mapView1_TrackerMoved`.
- `EU2MapCodecDev/EU2MapEditorControls/` provides the map controls, navigation and selection handling. Single-province selection must be connected to the editor panel.
- `Maptools/MapView/ProvinceLayerPainter.cs` provides a province information overlay.
- `EU2Edit/` reads map projects and FTG installation files.
- `EU2/Data/FtgProvinceDocument.cs` preserves unchanged source text when editing province definitions. Its parsing approach can inform a scenario document implementation, but the existing document type is province-specific.

The inspected code does not contain a complete FTG scenario reader/writer. The viewer is a UI foundation, not an existing scenario editor. Editing scenario data does not require PSD, PixiEditor or changes to province shapes.

## First usable version

| Field | Source and destination |
| --- | --- |
| Owner | Country `ownedprovinces` list in an included `.inc` file |
| Controller | Country `controlledprovinces` list, independent of ownership |
| National claims | Countries' `nationalprovinces` lists; multiple claims are allowed |
| Population and fortress | The owning country's `city` block |
| Buildings | Supported fields in the `city` block |
| Capital | City `capital` setting, with country consistency checks |
| Religion and culture | Defaults from `Db/Map/provinces.txt`, combined with scenario overrides |

The panel should show province ID, name, effective values and each value's source file. Distinguish inherited defaults from explicit scenario overrides. Editing a value for one scenario should update that scenario's override rather than silently changing the common province database.

## Data model and saving

Scenario data is spread across files linked by `include`. Load the complete include graph with correct ordering and mod/base-game path resolution. Detect missing files and include cycles. Index provinces and countries while retaining the source blocks and file locations.

Transferring a province requires removing its ownership entry from the old country, adding it to the new country and moving its city block. Controller changes require an independent choice. Handle capitals and stationed units consistently; do not remove other countries' claims as a side effect.

Separate the implementation into scenario documents, an indexed scenario model, undoable editing operations, the map/panel interface and a source-preserving writer. Keep comments, unknown fields, encoding and line endings. Prepare all files in a multi-file edit before replacement, retain originals and support rollback after a write failure. Report conflicting owners and references to nonexistent province IDs.

## Implementation order when resumed

1. Load the 1337 scenario and its includes; build province and country indexes.
2. Connect map clicks to a read-only panel showing current values and their sources.
3. Add population and fortress editing, then save the changed blocks while retaining other content.
4. Add ownership transfers, independent controller changes and national-claim editing.
5. Add owner colors, province search and the remaining fields in the first-version table.

Unit editing, full country editing, diplomacy, wars and events can follow later. Drawing province borders is a separate feature set.

## FTG compatibility and current evidence

Native `provinces.txt` support and increased province capacity remain priorities of the map toolchain. The scenario editor must use the IDs available in the selected map. It does not remove engine limits: the established FTG allocation limit is 10000 province slots.

A separate Gloriana_WatK4_MapTest144 mod was prepared with 144 added 16 x 16 provinces, IDs 2020-2163, assigned to Byzantium in the 1337 scenario. The user confirmed loading the scenario and seeing the island when using the diagnostic launcher. A previous scenario-start crash remains unexplained and must not be described as fixed.

Resume with the include-aware scenario reader and a read-only province panel. Implementation awaits the user's return to this topic.
