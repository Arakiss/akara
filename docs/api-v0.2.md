# AKARA0.2 shared Core/API contract

This contract extends0.1; existing routes and JSON fields remain valid. Public DTOs live in `Akara.Core`, namespace `ForjaHoradrica`. HTTP keeps token/origin guards; the CLI calls the same Core directly.

## Inventory and mercenary: first implementation vertical

`POST /api/preview` accepts `itemOperations`:

```json
{"sessionId":"...","itemOperations":[
 {"operation":"copy","itemId":"i0","clientId":"copy-oculus","destination":{"owner":"character","page":"stash"}},
 {"operation":"equip","itemId":"copy-oculus","destination":{"owner":"character","page":"equipped","slot":"weapon","weaponSet":2}},
 {"operation":"move","itemId":"m0","destination":{"owner":"character","page":"stash","x":0,"y":0}},
 {"operation":"delete","itemId":"i8"}
]}
```

`owner`: character or mercenary. `page`: inventory,stash,cube,belt,equipped. `slot`: helm,armor,weapon,shield,amulet,ring1,ring2,gloves,belt,boots. `weaponSet`:1 or2, for the character's weapon/shield only. Coordinates are optional for stored items: omitted means native-grid auto-placement; explicit coordinates must fit without overlap. Belt uses x as native slot index and y0. Mercenary destinations require equipped and a legal native slot. Replaced equipment moves to the personal stash, without destruction. Two-handed equipment reserves the off hand as metadata; no phantom object is created.

Item IDs are session-source IDs iN/mN. They are resolved before collection mutations. Preview summaries retain these IDs. New items/copies use the supplied unique clientId, or an automatically assigned ID. An operation may reference a clientId created by an earlier operation in that request. Each preview replays the complete draft from the immutable source; undo/redo edits the request, never the source file.

Inspection adds `activeWeaponSet:1|2`. An item adds `owner`, `weaponSet`, `twoHanded`, `equipSlots`, and native location data. `PreviewRequest.activeWeaponSet` explicitly switches sets. Native body4/5 are the active set;11/12 are inactive. Core translates I/II using the header WeaponSwitch, and changing the active set swaps both body locations and the corresponding skill headers.

Mercenary inspection: `{exists,dead,typeId,nameIndex,experience,level,act,typeName,aura?,allowedSlots,items}`. Optional computed resources/attributes/resistances are shown only when their native formula is verified. Reimagined permits the full nine-slot mercenary equipment layout; classic profiles retain their native supported slots. Existing `mercenary:{hirelingId,level,resurrect}` and gear.mercSlot remain supported.

`GET /api/catalog?sessionId=...&type=hireling` returns entries with native id/name/act/aura/version; rows are selected for the current character level. Other catalog types remain unique,set,runeword,base,prefix,suffix,stat,skills.

## Persistent plans for CLI

`DetachedPlanDocument` is a typed, versioned local file. Fields: schemaVersion1,createdAt,sourcePath(nullable for a new character),expectedSourceHash,profile{excelPath,stringsPath,name,fingerprint},saveVersion,candidate(byte array serialized as base64),candidateHash,diff,inspection.

Core methods:

- `CreateDetachedPlan(OpenRequest,PreviewRequest)` opens a selected file, prepares a candidate and detaches it.
- `DetachPlan(sessionId,planId)` detaches an existing valid in-memory plan.
- `ValidateDetachedPlan(document)` checks schema, profile fingerprint, source hash, save version, candidate hash/checksum, binary stability, placements and item stats.
- `ExportDetachedPlan(document,destinationPath?)` writes a new copy without overwrite.
- `ApplyDetachedPlan(document,expectedHash,destinationPath?)` uses the same backup/atomic-write/game-closed guards as the UI. A new character is created with CreateNew, never replacing a file.

Do not commit detached plans or private fixture files to the repository; they can contain local paths and character data.

## Implemented additional verticals

- World: `PreviewRequest.world={quests:[{difficulty,questId,state?,flags?}],waypoints:[{difficulty,id,active}],activeDifficulty?,act?,maxDifficulty?}`. Metadata comes from `GET /api/world?sessionId`. Quest states: notStarted,started,completed,rewarded. Flags is an optional advanced ushort; rewards and skill/stat budgets are reconciled after applying the complete draft.
- Create: `POST /api/create {profileId,className,name,level?,version?}` creates an in-memory draft from native data; default level1/version105. Clone: `POST /api/clone {sessionId,name,planId?}` creates an independent draft. Both return normal inspection with `isNew:true` and no source file.
- New-character save uses export or `POST /api/commit {sessionId,planId,destinationPath}` and never overwrites. Creating directly under Saved Games requires the game closed.
- Designer: `PreviewRequest.gear` will also accept typed quality/affixes/stats/socket children; generation validates native binary limits and reports unsupported property functions rather than ignoring them. Item blueprints are versioned structured JSON with native base/quality/stats/layers/socket children and profile metadata, without character headers.
- Presets/loadouts: `GET /api/presets?profileId` supplies native class-specific manifests. Existing orbe-tal-oculus is retained; additional class presets are added only after native validation.

All these verticals are implemented. Public Core models are authoritative for machine-readable fields. Creating a new character uses the canonical version105 model; opening existing characters accepts versions97–105 only when their original bytes roundtrip exactly.

## Loadouts and local library

`LoadoutDocument` contains schemaVersion1,name,className,saveVersion,profileFingerprint, optional level/attributes/skills/world/mercenary/hotkeys/hotkeySides/buttons/activeWeaponSet, and entries[{destination,blueprint}]. Export captures character equipmentI/II, active inventory charms and mercenary equipment. It omits utility items and character identity flags.

Core: `ExportLoadout(sessionId,name,planId=null,includeCharacter=true)`. Apply with `PreviewRequest.loadout`, `loadoutIncludeCharacter=true`, `loadoutKeepPrevious=true`. Existing active gear and charms move to the personal stash; false explicitly deletes them within the draft. A loadout is a reusable build: it preserves the target's XP when the level is unchanged and uses the native threshold when changing level. Clone copies exact character progress instead.

HTTP library routes:

- GET `/api/loadouts`; GET `/api/loadouts/{id}`.
- POST `/api/loadouts/save` with {sessionId,name,planId?,includeCharacter}; returns {id,path,document}.
- POST `/api/loadouts/export` with the same body; returns the document without saving a library entry.
- POST `/api/loadouts/import` with {sessionId,document}.
- DELETE `/api/loadouts/{id}` deletes only that local library entry.

The server accepts `--data-dir PATH` for isolated tests or local configuration. The default library and backup directory is LocalApplicationData/AKARA. No game data is copied into the library or repository.

## Final field semantics

- Inspection includes profile.classes, saveDirectories, inventoryGrid,stashGrid,cubeGrid,beltGrid, activeWeaponSet, item owner/equipSlots/twoHanded/requirements and native catalog IDs/encoding bounds.
- Header assignments accept32767 as the canonical unbound skill and65535 as a legacy clear alias. This does not permit those IDs as hard skill points. All16 loadout hotkeys and their left/right sides are retained. `hotkeySides` is optional on a preview.
- `PreviewRequest.experience` validates the class XP table and synchronizes level when level is omitted. `status:{hardcore?,dead?,ladder?}` changes explicit local header flags; the realm/local identity field is read-only.
- Designer stats can replace main stats, runewordStats, and setBonusLists/setBonusMask explicitly. Values use decoded native units; per-level values are normalized, and rawValue is available for exact native units. Paired stats and requested physical fields are checked after serialization.
- Defense, durability and maxDurability are final stored values, applied after ethereal conversion. SocketCount omitted preserves native sockets; zero clears them for a non-runeword. Occupied sockets must be contiguous. Runewords retain their native rune order.
- PersonalizedName is an optional artisan signature of up to15 printable ASCII characters. It is saved as native item personalization.
- Complete presets include skill/stat quest rewards, the three consumed Anya scrolls and three Golden Bird rewards. Golden Bird adds20 base life per newly granted reward; reapplying the same quest state cannot add life twice.
- The hybrid Sorceress preset is squad-sorceress; the older cold-only orbe-tal-oculus remains available. Paladin Hammer and Smite hotkeys use the left side while Concentration uses the right side.
