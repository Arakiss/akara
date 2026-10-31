# Architecture

AKARA 0.3 has a native WPF desktop client and a JSON CLI over one character-editing Core. Both call Core in-process on Windows/.NET 10. The self-contained desktop executable opens its own window; it does not start a browser, WebView or HTTP server.

| Component | Responsibility |
| --- | --- |
| `Akara.Core` | Profiles, creation, transformations, validation, item encoding, plans, loadouts, and guarded persistence. |
| `Akara.Desktop` | WPF views/styles, native file dialogs, asynchronous ViewModels and offscreen test/capture modes. |
| `Akara.Cli` | JSON commands, persistent plans, batches, and automation over Core. |
| `ForjaHoradrica.Server` | Retained legacy 0.2 loopback API; excluded from native distribution. |
| `ForjaHoradrica.Web` | Retained legacy 0.2 React client; excluded from native distribution. |

The `ForjaHoradrica` technical namespace remains for compatibility; product names are AKARA and `akara-cli`.

## Runtime and local storage

The desktop's `NativeEditorBridge` calls `EditorService` on background tasks. ViewModels apply the results to observable native models on the UI dispatcher and gate save commands behind a valid reviewed plan. Windows file dialogs are abstracted for tests. No transport or API token sits between the native client and Core.

The package needs no separately installed Node.js or .NET runtime. There is no remote service or account. NuGet is a native build dependency; npm belongs only to the archived web client.

Core normally stores settings, backups, exports, and loadouts under `%LOCALAPPDATA%\AKARA`. Desktop and CLI accept `--data-dir DIRECTORY` for isolated instances. Core's optional save-root injection permits private behavior tests to discover and write only their temporary fixture copies. Generated plans and JSON documents can contain local paths and character data; they are private artifacts, not source fixtures.

## Native data profiles

`GameData` reads TXT tables and applicable strings from the user's installation. These provide progression, skills, item bases, dimensions, properties, requirements, hirelings, and native limits. The profile hashes table files and runeword string data; operations reject a changed profile. A persistent plan records the profile fingerprint.

Automatic discovery targets a typical Reimagined installation. Another matching local table directory can be registered explicitly. No game tables, archives, save templates, native screenshots, or game artwork are distributed in the repository.

## Draft, preview, and write

1. `Open` checks signature, version, and checksum, retaining immutable source bytes and their SHA-256. Editing requires an exact original read/write roundtrip.
2. `Create` builds a version 105 model for the selected class from profile data. `Clone` makes an independent draft from an existing session or valid plan. Neither writes a character file.
3. Each preview reads a fresh model from its source snapshot and replays the complete typed request. Unspecified state is preserved. Undo/redo changes the request rather than applying inverse binary patches to a saved character.
4. Validation checks attribute/skill budgets, progression, prerequisites, stat ranges and layers, placement, duplicate slots, equipment compatibility, socket contents, and stat uniqueness. Candidate bytes must pass checksum and a stable read/write roundtrip.
5. A valid preview retains candidate bytes behind a plan ID. The interface requires review before saving. CLI can detach a versioned document containing hashes, profile, candidate bytes, inspection, and diff.
6. Importing a detached plan rechecks its format, profile fingerprint, candidate hash/checksum, native validity, roundtrip, and unchanged source hash. A changed source requires opening and planning again.
7. Export creates a file without overwriting. Writing into the discovered Saved Games tree requires a closed game; copies outside that tree can be exported while playing.
8. Commit requires an unchanged source hash and closed game. It creates and verifies a backup, flushes a temporary file in the source directory, rechecks the source and game state, then uses atomic replacement. Restore follows that path and backs up the displaced version.

Batch creation prepares and validates all candidates before creating its first file. On failure, rollback removes only files created by the batch which still have the expected hash; changed files are preserved and reported.

## Inventory and mercenary

Operations use stable source-session IDs and client IDs for new objects. IDs are resolved before collection mutations, so deleting an earlier item does not retarget a later operation. Copies get fresh seeds; edits preserve the existing seed and location unless a move is requested.

Stored items use profile dimensions and grids. Coordinates must fit without overlap; omitted coordinates request automatic placement. Replaced equipment moves to the personal stash. Lack of space is an error, not permission to delete an item.

The public model uses logical weapon sets I and II. Native physical slots 4/5 are active and 11/12 inactive. Core maps them through the active-set bit; switching sets swaps slots and corresponding skill headers. A two-handed weapon reserves the other hand through metadata without creating a second item. Class-specific dual-wield and off-hand restrictions remain validated.

Mercenary identity, state, and XP remain unless explicitly changed. Allowed slots come from profile capabilities. Reimagined permits nine equipped pieces: weapon, armor, helm, amulet, gloves, belt, boots, and two rings. A two-handed weapon also occupies the off-hand display, so visible rectangles differ from actual pieces. Other profiles retain their supported slot rules.

The GUI exposes inventory and mercenary controls with non-drag alternatives. Derived mercenary information appears only where its native calculation is implemented; base character resources are distinguished from game-calculated equipment totals.

## Items and workshop

`Core/NativeItemFactory.cs` independently constructs items from local definitions. It handles IDs, fixed-point values, layered stats, set masks, socket children, and runeword string IDs. The engine derives rune/gem effects from socket children; those effects are not duplicated in the parent's saved modifiers.

`ItemDesigner` supports normal/inferior/superior/magic/rare/crafted/unique/set and runeword design, affixes, explicit stats, personalization, physical fields, and sockets. Explicit values remain bounded by native encoding. Unsupported functions or missing property definitions are rejected rather than discarded. A custom save item uses existing definitions; it does not install a base, asset, or mod-table row.

A blueprint contains individual item data and profile/save-version metadata without a character header. Import validates compatibility and assigns an independent identity. Plans, blueprints, and loadouts are private character data.

## World, presets, and loadouts

The world editor exposes quest flags/states, waypoints, active difficulty, act, and progression by difficulty. Rewards reconcile permanent resources and point budgets. Consumed reward flags matter separately from quest completion; previews validate the resulting character rather than just changing a display label.

Core supplies nine Reimagined presets: eight class builds and the original Frozen Orb build. They use names and IDs from the selected profile, with guides returned as metadata. Presets are transformations, not distributed save templates. New characters are constructed from models and native progression data.

Loadouts capture both weapon sets, mercenary gear, and active charms, with optional character/progression data. They record profile compatibility, support library storage and JSON import/export, and prepare the same preview flow. They reuse a build rather than restoring the donor's exact XP: the same level preserves destination XP; a level change selects the native XP threshold. Clone retains exact character XP, and explicit experience editing is separate. Previous equipment stays in the stash by default; explicitly discarding it is a visible change using the normal guarded save path.

## Compatibility and verification

D2SSharp 0.0.22 parses and serializes saves. Existing versions 97–105 require per-file exact roundtrip checks; AKARA does not convert formats. New-character creation targets version 105 and all eight classes.

Core and CLI checks use private tables and copied version 105 saves. Creation and presets have binary/roundtrip checks; these do not establish an in-game pass for every class. Versions 97–104 have not each been exercised with fixtures. The public browser smoke exercises the main 0.2 workflows against a real local API, including invalid native encodings, reversible inventory/socket changes, blueprint/loadout exchange, and private-file commit/backup/restore.

Public CI builds WPF, CLI and data-dependent runners without distributing their inputs. Native capture mode arranges and renders the same `WorkbenchView` used by the window through `RenderTargetBitmap`; no window is shown and no desktop input is injected. Captures include actual `PresentationFramework` and layout metadata. Native behavior tests execute on the STA dispatcher, using dialog stubs and isolated Core state. Screenshots still require visual inspection at target dimensions; structural PNG checks are not a design-quality verdict.

See [integration tests](../tests/README.md) and [CLI](cli.md). The retained [browser checks](../tests/browser/README.md) and [API](api-v0.2.md) document the historical 0.2 client and do not establish native UI coverage.
