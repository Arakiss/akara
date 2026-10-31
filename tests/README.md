# Backend integration checks

This project is a console integration runner, not a test-discovery project. It verifies save-file behavior using private fixtures and fails with a nonzero exit code when a check fails. With missing inputs it prints the requirements and exits with code2; missing fixtures are not reported as a successful test run.

CI can restore and build both projects without a game installation or save files:

```sh
dotnet restore src/ForjaHoradrica.Server/ForjaHoradrica.Server.csproj
dotnet build src/ForjaHoradrica.Server/ForjaHoradrica.Server.csproj --no-restore
dotnet build tests/ForjaHoradrica.Tests.csproj
```

Running the integration checks requires your own D2R Reimagined3.1 data and two private copies of saves. Neither game data nor save fixtures are distributed in this repository:

- `AKARA_TEST_EXCEL`: the directory containing the installed mod's native.txt tables, including charstats.txt, skills.txt, itemstatcost.txt and hireling.txt. Keep the original mod data/local/lng/strings directory in its normal relative location; runeword creation reads item-runes.json there.
- `AKARA_TEST_FIXTURES`: a directory outside the repository containing `sorceress.d2s` and `warlock.d2s`. These are copies you provide, not files created by the runner. The internal character names can be different from these fixture filenames.
- The Sorceress fixture must be a fresh version105, level1 Reimagined character with no spent skill points or completed quests. The Warlock fixture must use version105 and a level below99.

PowerShell example, with paths you choose:

```powershell
$env:AKARA_TEST_EXCEL = 'D:\Games\D2R\mods\Reimagined\Reimagined.mpq\data\global\excel'
$env:AKARA_TEST_FIXTURES = 'D:\PrivateFixtures\AKARA'
dotnet run --project tests/ForjaHoradrica.Tests.csproj
```

Alternatively pass three arguments, in this order:

```sh
dotnet run --project tests/ForjaHoradrica.Tests.csproj -- /private/sorceress.d2s /private/warlock.d2s /private/Reimagined/data/global/excel
```

The runner copies its inputs into a unique operating-system temporary directory. All export, replacement, backup and restore checks operate on those copies. It verifies that the supplied files remain unchanged and prints the temporary artifact directory for inspection. It does not locate or modify Saved Games automatically.

The current suite contains86 checks. These cover binary preservation, intermediate XP, native skill limits, quest rewards, paired item stats, runewords, all eight native classes, nine build presets, stable inventory IDs, blueprints, custom crafted items, both weapon sets, full mercenary equipment, loadouts, status flags, optimistic concurrency and backup/restore. It does not claim that every save version has been tested in the game engine.
