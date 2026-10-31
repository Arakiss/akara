# Local game art

AKARA imports optional item and skill images from the user's installed game.
No standalone Blizzard or Reimagined sprites are included in the repository or release.
Documentation screenshots show the app with a fictional demonstration character;
they are not an asset library. Game artwork remains the property of its owners.
Only the decoder and the MIT-licensed CASC reader are distributed.

`LocalGameArt.WarmupAsync` runs outside the WPF dispatcher. It reads the selected
mod's loose files first, then the local game's CASC archives. It writes derived
PNG images and their provenance manifest to the selected AKARA data directory's
`game-art` cache, outside the game. UI image lookups use frozen memory buffers:
they do not open archives or decode files while rendering.

The source key includes a renderer version, the installed build-info timestamp,
and timestamps of the selected mod's item maps, color presets and appearance
tables. The manifest records each item's native sprite path, source SHA-256,
native color identifier and resulting local PNG. Cache contents are private game
data and must not be committed or bundled. Missing or unsupported pictures use a
neutral document marker and the actual item name; generated item art is not used.

Resolution uses the installation's `data/hd/items/items.json`, `uniques.json` and
`sets.json`. Unique/set names select their actual HD entry; random inventory
graphics select the saved variant. Skills use the native class sprite and
`SkillDesc.IconCel`. RGBA `.sprite` version31 is supported with validated frame
dimensions and horizontal stride. Unsupported versions are reported, not guessed.
HD color presets and per-sprite masks/overrides come from the installed
`data/global/ui/layouts/globaldatahd.json`. The CPU conversion was checked against
the actual advanced-color shader in installed D2R3.3 `data/hd/shaders/ps_ui_p.bin`:
range distances wrap on all three HSV channels, hue is offset, saturation/value
use gains without an early clamp, and tint multiplies in linear RGB between sRGB
conversions. This corrected the green hood, dark purple armor and blue charms
against a private game screenshot. Final display gamma/HDR is managed by WPF and
Windows; the app does not claim pixel-identical screen capture output.

The shader was read from CASC and inspected locally using the Windows SDK shader
disassembler. Neither its bytes nor its disassembly are included in this project.
The observed source SHA-256 was
`4D851DFE052B22A7D09CEE21D8A6B63EF3D376ED6F768D39D704ABC710528B36`.

## Native library provenance

- Upstream: https://github.com/ladislav-zezula/CascLib
- Commit: `6b889ec8f421cbc9baccec296c582362ab917887`
- License: MIT, copyright 2014 Ladislav Zezula; full notice in
  `licenses/CascLib-MIT.txt`.
- Build: Visual Studio2022 C++ Build Tools, `Release|x64`, upstream
  `CascLib_dll.vcxproj`; `RuntimeLibrary=MultiThreaded` (`/MT`), with static CRT.
- Only source patch: resource include `afxres.h` becomes `winres.h`, avoiding an
  unnecessary MFC resource-header dependency. Reader code is unchanged.
- Included DLL SHA-256:
  `3519812D005B444F8743EA65129440773BBD710D03DD05CB921196B87A77576F`.
- Rebuild with `scripts/Build-CascLib.ps1`. The script fetches that exact source
  commit, compiles the library, and copies the DLL plus its MIT notice.

## Format references

The C# decoder is implemented in this repository. Format descriptions and
read-only import architecture were checked against these primary source projects:

- https://github.com/eezstreet/D2RModding-SpriteEdit
- https://github.com/scorchmuff1n/D2R_Jamella_Editor/blob/main/hero_editor/sprite.py
- https://github.com/scorchmuff1n/D2R_Jamella_Editor/blob/main/hero_editor/gameart.py
- https://github.com/scorchmuff1n/D2R_Jamella_Editor/blob/main/hero_editor/gamedata.py

The last project is a modern, separate editor. It is not the historical Jamella
application. No external editor executable is installed or launched by AKARA.
