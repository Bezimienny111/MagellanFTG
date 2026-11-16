# Changelog

## v0.1.0-ftg-preview.2 — 2026-11-16

PSD adjacency regeneration no longer adds border/outline markers as neighbours.
Previously, adding marker 2020 could increase province capacity from 2020 to 2021,
causing native injection to treat the old marker as an extra province in dense regions.

- Reject neighbour IDs at or above the map's defined-ID threshold before looking up
  their province terrain, in both ordinary and river adjacency lists.
- Filter markers before checking ID-grid capacity, including old markers left after
  a capacity increase. Keep TI in slot zero and preserve all valid high IDs.
- Reject 256 real IDs before changing a region. Forced builds now consistently skip
  overflowing regions without throwing or replacing their previous contents.
- Identify rebuilt tools as Magellan FTG 0.1.0 Preview 2, assembly version 0.1.0.2.

Verification covers land/river marker adjacency, capacity/threshold differences,
current and stale markers, high IDs 2020 and 9999, full and empty regions, and
overflow handling. A real WatK4 river tile is exported to PSD, imported with forced
adjacency regeneration, and injected into a separate native directory. Existing
extraction, native/PSD roundtrip and three-lightmap migration checks remain included.

This fix prevents new marker neighbours and excludes markers during grid rebuilding.
It does not automatically remove every old invalid adjacency or reduce a previously
inflated province capacity. Prefer reimporting edits into a clean backup with this
release. For an already affected project, inspect its defined IDs and adjacency data
before recovery; do not blindly replace 2020, which can be a legitimate province ID.

The FTG engine capacity limit remains 10000 slots and each ID-grid region supports
255 non-TI IDs. This release has no new in-game compatibility claim.
