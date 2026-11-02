# Work on AKARA

AKARA is a native WPF offline Diablo II: Resurrected character editor. The desktop GUI and CLI call the same `Akara.Core` validation and persistence code directly. Do not introduce a browser, WebView or local HTTP host into the desktop client. Keep game data local: this repository must not contain Blizzard/mod TXT tables, saves, item binary fixtures, screenshots from private sessions, tokens, or machine-specific paths.

## Use the installed CLI

Start with `akara-cli doctor`, `saves`, `presets`, and `schema commands`. Output is JSON by default; diagnostics and structured errors go to stderr. See `docs/cli.md`. Read `schema preview` before constructing changes, and use `catalog` for IDs from the selected profile. Do not guess skill IDs, unique indexes, native property encoding, or vanilla item rolls for a mod.

Prefer `plan SAVE --changes @changes.json --out plan.json`, review its diff, then `validate` and `apply`. A plan is portable between processes and deliberately fails if the source or data tables changed. Reopen and replan on a conflict. Exporting or cloning never overwrites an existing destination. No interactive confirmation is required for an explicitly requested CLI command.

Treat generated plans and blueprints as private save data. Do not commit them. Do not edit live saves with ad hoc byte patches when a Core transformation can express the change. If adding an unsupported transformation, implement and validate it in Core first.

## Build and verify

Prerequisites for native development: .NET 10 SDK and PowerShell 7 on Windows. Portable releases include the runtime and need neither. Node/npm are needed only for the retained legacy browser client.

```powershell
dotnet build src/Akara.Cli/Akara.Cli.csproj
dotnet build src/Akara.Desktop/Akara.Desktop.csproj
dotnet build tests/ForjaHoradrica.Tests.csproj
```

The integration runner needs your own local game tables and copied saves. `AKARA_TEST_EXCEL` selects the TXT directory; `AKARA_TEST_FIXTURES` selects a private directory containing `sorceress.d2s` and `warlock.d2s`. Missing fixtures must be reported explicitly, never counted as a passing integration test. The runner works on temporary copies.

`scripts/Publish.ps1` builds both native desktop and CLI standalone executables. `Test-Native.ps1` runs the actual WPF tree offscreen at multiple dimensions, verifies native framework metadata, and exercises the ViewModels. Do not substitute HTML, design mockups or screenshots of a different implementation for native application evidence. Captures/self-tests must not show a window, inject desktop input or write live characters. Private-file checks copy fixtures into an isolated save root and data directory first.

## Invariants

- Preserve fields absent from a change request, current experience when the level stays equal, unrelated items, and mercenary identity unless the operation explicitly changes them.
- Validate checksum, byte-exact roundtrip, table fingerprint, native grids and overlaps, skill/attribute budgets, prerequisites, stat bit ranges/layers, paired stats, sockets, and equipment compatibility.
- Handle weapon sets as logical setsI/II. D2R swaps physical slots4/5 and11/12 when its active-set bit changes.
- Reimagined permits full mercenary gear. Read profile capabilities; do not assume the three vanilla slots are the whole inventory.
- Back up, verify, recheck the source and closed-game state, then replace atomically. Never bypass a stale-source conflict.
- Custom save items use existing native bases and encodable properties. Adding a new base, asset, or unique definition to the game requires a separate mod-data workflow.

Keep machine-oriented identifiers stable. English is the default desktop language; Spanish is optional and persisted. Route presentation text through the embedded language catalog and preserve user-authored and native identifiers. Include accessible non-drag controls for inventory operations. Avoid placeholder actions and do not claim engine testing when only serialization tests ran.
