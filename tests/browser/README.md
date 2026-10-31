# Browser checks

The Playwright script exercises the real local interface and API with private, disposable files. It checks inventory movement, copy/delete, weapon sets, mercenary equipment, world edits, new characters/cloning, native templates, workshop properties and sockets, blueprint exchange, loadout save/import/apply/delete, and independent `.d2s` export. It also checks invalid property rejection, undo/redo, modal focus, keyboard tabs and a 1024px layout.

When Diablo II is closed it saves and restores its disposable working copy, verifies the backup against the original bytes, and creates a new character only in the private output directory. The supplied fixture is never written. A pass does not establish every edge case or in-game character behavior.

## Install

From the repository root, use Node.js 24 and npm:

```powershell
npm ci
npx playwright install chromium
```

The root lockfile pins Playwright **1.62.1**. Alternatively, set `AKARA_TEST_BROWSER_CHANNEL=msedge` to use installed Microsoft Edge; Chromium installation is then unnecessary for that run.

## Start an isolated host

Use a built or packaged host with the actual interface embedded. Keep test state separate from a normal session:

```powershell
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('akara-browser-' + [guid]::NewGuid().ToString('N'))
$testState = Join-Path $testRoot 'state'
$testOutput = Join-Path $testRoot 'results'
Write-Host "Test output: $testOutput"
dotnet run --project src/ForjaHoradrica.Server/ForjaHoradrica.Server.csproj -- --port 5273 --no-browser --data-dir $testState
```

The host prints `AKARA_READY` followed by a loopback URL containing its temporary token. Use that URL in the test terminal. Do not commit the real URL or token. For a portable package, run `Akara.exe` with the same host arguments instead of `dotnet run`.

Build and embed the interface first; a host displaying only the build placeholder cannot run this test. `scripts/Publish.ps1` prepares a packaged build.

## Private inputs

Set these environment variables in the terminal that runs the test:

| Variable | Required | Meaning |
| --- | --- | --- |
| `AKARA_TEST_URL` | Yes | The running host's loopback URL; it can contain the token. |
| `AKARA_TEST_TOKEN` | If absent from URL | Temporary token for that host run. |
| `AKARA_TEST_SAVE_PATH` | Yes | Absolute path to a copied `.d2s`, outside Saved Games and the repository. |
| `AKARA_TEST_DEST_DIR` | Yes | Private output directory outside Saved Games and the repository. |
| `AKARA_TEST_EXCEL` | Optional | Local TXT profile directory if discovery does not find the matching mod. |
| `AKARA_TEST_BROWSER_CHANNEL` | Optional | For example, `msedge` for installed Microsoft Edge. |

Use a Reimagined 3.1 format 105 fixture that passes the exact roundtrip gate and contains at least one inventory item. A fully equipped companion, including a two-handed weapon, enables the mercenary equipment checks. Checks that cannot run because the fixture lacks a companion or the game is open appear explicitly under `skipped`. No saves or installed tables are included. The script rejects source/output paths containing a Saved Games directory and makes a disposable source copy inside its output directory. The wizard and template assertions require the profile's eight classes and nine built-in templates.

In the test terminal, set your actual private source path and the URL printed by the host. Reuse the `$testOutput` value from the host setup for `AKARA_TEST_DEST_DIR`. Keep these session values out of source control.

```powershell
$env:AKARA_TEST_URL = 'PASTE_THE_LOCAL_AKARA_READY_URL'
$env:AKARA_TEST_SAVE_PATH = 'ABSOLUTE_PATH_TO_YOUR_PRIVATE_SAVE_COPY.d2s'
$env:AKARA_TEST_DEST_DIR = 'PASTE_THE_TEST_OUTPUT_DIRECTORY'
# Set AKARA_TEST_TOKEN only when the URL does not contain the token.
# Optional: $env:AKARA_TEST_BROWSER_CHANNEL = 'msedge'
npm run test:browser
```

## Results and limits

Each run creates a `browser-*` subdirectory with `report.json` and screenshots. Failure produces a nonzero exit and a failure report; missing inputs fail explicitly. Error messages mask the local token, but screenshots and reports can still contain character data and paths. Keep all output private.

SHA-256 checks compare the supplied fixture before/after and require the disposable source to be restored exactly. The suite creates temporary library entries and removes only those entries through the UI. A failed run may leave test entries in the isolated data directory; keep that directory separate from normal app data. Commit and restoration checks require Diablo II to be closed. The browser always closes; the host remains running so you can inspect its results, then exit it through the interface.
