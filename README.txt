Jamella · Tributo 0.7.0 (AKARA project)
Native offline Diablo II: Resurrected character editor for Windows

Open Akara.exe. The portable package includes the runtime.
English is the default, regardless of your Windows language.
Select Language > Español for Spanish or Language > English for English.
Your choice is remembered. Language changes preserve your draft and review
state. Correct invalid input before changing languages.

OPEN AND EDIT

Double-click a local character or select Open… to choose a .d2s file.
Use the matching game/mod data profile. The tables and artwork are read
from your own installation and are not included in this package.

Character and Skills edit attributes, level and skill points. Equipment
includes inventory, stash, cube, belt and both weapon sets. Mercenary
shows all slots supported by the profile. World edits quests and waypoints.

To modify an item: Equipment > select item > Edit item… > Properties.
Select a property, then change the value below the table. Expand Add a
property to search for and add a native modifier. Stage changes validates
the design before closing the editor. Advanced parameter identifies a
native skill, class or charge; keep it unchanged when adjusting a value.
Use automatic catalog properties replaces your custom table.

SAVE

Review and save… (Ctrl+S) shows the proposed changes. Check them, then
select Save. Close Diablo II before replacing a save. The editor verifies
a backup, checks the source hash and replaces the file atomically.

Export copy writes a separate file without overwriting its destination.
Exports outside Saved Games are allowed while the game is running.
Backups can be restored from Changes. Restore also backs up the current file.

BUILDS AND AUTOMATION

Library saves reusable builds with JSON import/export. Previous equipment
is kept in the personal stash by default when applying another build.
Nine Reimagined presets include a guide to skills, buffs and weapon sets.

The standalone akara-cli uses the same Core and JSON contracts. Start with
help, doctor and schema commands. Use plan, inspect the diff, validate,
then apply. See CLI.md and examples.

COMPATIBILITY AND LOCAL DATA

Developed with D2R Reimagined 3.1. Save versions 97–105 require byte-exact
roundtrip checks. Creation uses version 105 and supports eight classes.
Binary verification is not a guarantee that every build works in the game.
Custom items use existing native bases and encodable properties. Adding
new bases or artwork requires a separate mod-data workflow.

Online characters and shared stash files are not edited. Profiles, backups,
artwork caches, builds and desktop language preference stay local under
%LOCALAPPDATA%\AKARA. --data-dir isolates another instance's local state.
--language en or --language es overrides language for one launch without
changing the saved preference. Do not publish saves or private game data.

MIT License, copyright 2026 Arakiss. See LICENSE, licenses and TRIBUTO.md.
Diablo II and its assets belong to their respective owners. This project
is an independent tribute, not an official continuation of Jamella's editor.
