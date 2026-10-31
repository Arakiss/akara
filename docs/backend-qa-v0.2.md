# AKARA0.2 Core verification

The final Core source is frozen. The .NET10 build completed with zero warnings and zero errors. The integration runner completed86 checks with exit code0.

The checks use privately supplied native tables and save fixtures. Inputs are copied to unique temporary directories; all test replacements, backups and restores operate on those copies. No test writes to a real character file.

Covered behavior:

- Original and full-form no-op saves preserve bytes and intermediate XP.
- Native point budgets, skill prerequisites, attribute limits and level reductions are enforced.
- All eight classes are created from canonical version105 models without save templates.
- Nine presets validate: the eight class builds, including the hybrid Sorceress, plus the older cold Sorceress build.
- Quest rewards include consumed Anya scrolls and idempotent Golden Bird life rewards.
- Ordered inventory operations preserve source IDs; copies and imported blueprints receive new recursive seeds.
- Both weapon sets, hotkey sides and all nine mercenary items survive serialization. Paladin Hammer/Smite remain left bindings and Concentration remains right.
- Blueprint export resolves preview IDs. Crafted250ED, personalization, ethereal final values, paired stats and socket layout are verified against decoded output.
- World flags and waypoints are read from the candidate; unchanged state produces no artificial differences.
- Reusable loadouts preserve target XP at the same level, capture the complete equipment set, and save/read/delete through the isolated local library.
- Native32767 and legacy65535 clear sentinels are accepted only for assignments. Explicit local status edits validate.
- Persistent plans survive JSON serialization. Hash conflicts, game-open guards, backups, atomic replacement and restoration are checked.
- Repeated previews do not mutate the caller's typed request.

An independent native-item audit created and verified1,850 unique/set objects. Nineteen additional rows were rejected explicitly because their property codes are absent from the selected properties.txt:17 droponly_weapons, one magdam-rand and one skilltab-all. No guessed mappings were added. Catalog capability metadata uses the same supported-function list as the item factory.

The CLI and browser-interface checks are separate release gates. These results establish file and application behavior; they do not claim that every generated character has been played in the game engine. Existing-file support for versions97–105 requires an identical original roundtrip; the canonical creation fixtures and class presets are validated for version105 with Reimagined3.1 data.
