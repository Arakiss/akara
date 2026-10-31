export interface ClassMetadata { id: number; name: string; maxLevel: number; baseAttributes?: Record<string, number> }
export interface Profile { id: string; name: string; kind: string; excelPath: string; classes?: ClassMetadata[] }
export interface SaveEntry { path: string; name: string; profileId: string }
export interface Bootstrap {
  appName: string; version: string; gameRunning: boolean; profiles: Profile[];
  saves: SaveEntry[]; presets: { id: string; name: string; className?: string; description?: string }[]; defaultProfileId?: string;
  saveDirectories?: { profileId: string; path: string; label?: string }[]
}
export interface Attributes {
  strength: number; dexterity: number; vitality: number; energy: number;
  gold: number; stashGold: number; unspentStats: number; unspentSkills: number
}
export interface Skill {
  id: number; name: string; points: number; maxPoints: number; requiredLevel: number;
  prerequisites: number[]; tabId?: number; tab?: string; row?: number; column?: number
}
export interface ItemStat { id?: number; name?: string; label?: string; value?: number; displayValue?: string | number; layer?: number; hidden?: boolean }
export interface Item {
  id: string; code: string; name: string; quality: string; type: string; slot?: string;
  page?: string; x: number; y: number; width: number; height: number;
  stats?: ItemStat[]; sockets?: (Item | null)[]; runewordName?: string; baseName?: string;
  owner?: 'character' | 'mercenary'; weaponSet?: 1 | 2; twoHanded?: boolean; equipSlots?: string[];
  level?: number; quantity?: number; durability?: number; maxDurability?: number; ethereal?: boolean; personalizedName?: string;
  runewordStats?: ItemStat[]; setBonusLists?: ItemStat[][]
  identified?: boolean; defense?: number; prefixIds?: number[]; suffixIds?: number[]; rarePrefixId?: number; rareSuffixId?: number; setBonusMask?: number
}
export interface ItemDestination { owner: 'character' | 'mercenary'; page: 'inventory' | 'stash' | 'cube' | 'belt' | 'equipped'; slot?: string; weaponSet?: 1 | 2; x?: number; y?: number }
export interface ItemStatSpec { id: number; layer?: number; value?: number; rawValue?: number }
export interface ItemDesign {
  baseCode: string; quality: string; level: number; uniqueName?: string; setName?: string; runewordName?: string;
  prefix?: string; suffix?: string; prefixIds?: number[]; suffixIds?: number[]; rarePrefixId?: number; rareSuffixId?: number;
  ethereal?: boolean; identified?: boolean; personalizedName?: string; defense?: number; durability?: number;
  maxDurability?: number; quantity?: number; socketCount?: number; socketChildren?: (ItemDesign | null)[]; stats?: ItemStatSpec[]
  runewordStats?: ItemStatSpec[]; setBonusMask?: number; setBonusLists?: ItemStatSpec[][]
}
export interface ItemBlueprint { schemaVersion: number; saveVersion: number; profileFingerprint: string; code: string; name: string; quality: string; data: string; dataHash: string; description?: Item }
export interface LoadoutDocument { schemaVersion: number; name: string; className: string; saveVersion: number; profileFingerprint: string; level?: number; attributes?: Record<string, number>; skills?: { id: number; points: number }[]; world?: WorldChange; mercenary?: Changes['mercenary']; hotkeys?: Record<string, number>; leftSkill?: number; rightSkill?: number; activeWeaponSet?: 1 | 2; entries: { destination: ItemDestination; blueprint: ItemBlueprint }[] }
export interface LoadoutEntry { id: string; name: string; className: string; createdAt?: string; updatedAt?: string }
export interface PresetEntry { id: string; name: string; classId: number; className: string; description: string; level?: number; guide?: { opening?: string; strategy?: string; charms?: string; resists?: string; notes?: string[]; skills?: Record<string, number>; hotkeys?: { key: string; skill: string; id?: number }[]; alternatives?: { name: string; type: string; baseCode?: string }[] } }
export interface ItemOperation { operation: 'move' | 'equip' | 'copy' | 'delete' | 'create' | 'edit'; itemId?: string; destination?: ItemDestination; clientId?: string; gear?: GearChange; design?: ItemDesign; blueprint?: ItemBlueprint }
export interface Mercenary {
  exists: boolean; dead: boolean; typeId: number; nameIndex: number; experience: number; level: number;
  act?: number; typeName?: string; aura?: string; allowedSlots?: string[]; items: Item[];
  resources?: Record<string, number>; attributes?: Record<string, number>; resists?: Record<string, number>
}
export interface Hireling { id: string; nativeId: number; name: string; act: number; aura?: string; version?: number; supported?: boolean }
export interface CharacterSession {
  sessionId: string; sourceHash: string; sourcePath: string | null; profileId: string; fileName: string;
  character: { name: string; classId: number; className: string; level: number; experience: number;
    attributes: Attributes; vitals: { life: number; maxLife: number; mana: number; maxMana: number } };
  skills: Skill[]; items: Item[]; gameRunning: boolean;
  inventoryGrid?: { width: number; height: number };
  stashGrid?: { width: number; height: number }; cubeGrid?: { width: number; height: number }; beltGrid?: { width: number; height: number };
  activeWeaponSet?: 1 | 2; mercenary?: Mercenary; isNew?: boolean;
  questRewards?: { skillPoints: number; statPoints: number; anyaScrollsConsumed: number };
  progression?: { statsPerLevel: number; skillsPerLevel: number; maxLevel: number; baseAttributes: { strength: number; dexterity: number; vitality: number; energy: number } };
  validation: { canEdit: boolean; checksum: boolean; roundtrip: boolean }
}
export type GearType = 'unique' | 'set' | 'runeword' | 'magic' | 'base'
export interface GearChange {
  type: GearType; name: string; baseCode?: string; slot?: string; page?: string;
  prefix?: string; suffix?: string; sockets?: string[]; mercSlot?: string; ethereal?: boolean; level?: number; weaponSet?: 1 | 2; clientId?: string
}
export interface Changes {
  sessionId: string; name?: string; level?: number;
  attributes?: Partial<Pick<Attributes, 'strength' | 'dexterity' | 'vitality' | 'energy' | 'gold' | 'stashGold'>>;
  skills?: { id: number; points: number }[]; gear?: GearChange[];
  moveToStashItemIds?: string[]; preset?: string;
  itemOperations?: ItemOperation[]; activeWeaponSet?: 1 | 2;
  mercenary?: { hirelingId?: number; level?: number; resurrect?: boolean };
  world?: WorldChange;
  loadout?: LoadoutDocument; loadoutIncludeCharacter?: boolean; loadoutKeepPrevious?: boolean;
  questRewards?: { skills: boolean; stats: boolean; anya: boolean };
  unlockHell?: boolean; unlockWaypoints?: boolean; fillTomes?: boolean; fillBelt?: boolean
}
export type Difficulty = 'normal' | 'nightmare' | 'hell'
export type QuestState = 'notStarted' | 'started' | 'completed' | 'rewarded'
export interface Quest { id: string; name: string; act: number; flags: number; state: QuestState; rewardClaimed?: boolean; anyaConsumed?: boolean }
export interface Waypoint { id: number; name: string; act: number; active: boolean }
export interface WorldMetadata { activeDifficulty: Difficulty; act: number; maxDifficulty: Difficulty; difficulties: { id: Difficulty; quests: Quest[]; waypoints: Waypoint[] }[] }
export interface WorldChange { quests?: { difficulty: Difficulty; questId: string; state?: QuestState; flags?: number }[]; waypoints?: { difficulty: Difficulty; id: number; active: boolean }[]; activeDifficulty?: Difficulty; act?: number; maxDifficulty?: Difficulty }
export interface Preview {
  planId: string; canSave: boolean; validation: { errors: string[]; warnings: string[] };
  diff: { section: string; label: string; before: unknown; after: unknown }[];
  summary: CharacterSession
}
export interface CatalogEntry {
  id: string; name: string; code?: string; type: string; levelRequired?: number;
  supported: boolean; description?: string; slot?: string; nativeId?: number; label?: string; level?: number;
  saveBits?: number; paramBits?: number; saveAdd?: number; valShift?: number; minValue?: number; maxValue?: number;
  allowedTypes?: string[]; itemType?: string; maxSockets?: number; gemSockets?: number; equipSlots?: string[]; socketable?: boolean
}
export interface Catalog { entries: CatalogEntry[]; total: number }
export interface CommitResult { backupPath: string; outputPath: string; hash: string; summary: CharacterSession }
export interface ExportResult { outputPath: string; hash: string }
export interface BackupEntry { id: string; path: string; createdAt: string; hash: string }

export class ApiError extends Error {
  code: string
  details?: unknown
  constructor(message: string, code: string, details?: unknown) { super(message); this.code = code; this.details = details }
}
const token = new URLSearchParams(window.location.search).get('token')
export async function api<T>(path: string, body?: unknown, signal?: AbortSignal, method?: 'GET' | 'POST' | 'DELETE'): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (token) headers['X-Forja-Token'] = token
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  let response: Response
  try { response = await fetch(`/api${path}`, { method: method || (body === undefined ? 'GET' : 'POST'), headers, body: body === undefined ? undefined : JSON.stringify(body), signal }) }
  catch (error) {
    if (error instanceof Error && error.name === 'AbortError') throw error
    throw new ApiError('No se pudo conectar con AKARA. Comprueba que la aplicación sigue abierta.', 'CONNECTION')
  }
  const data = await response.json().catch(() => null)
  if (!response.ok) throw new ApiError(data?.message || 'No se pudo completar la operación.', data?.code || String(response.status), data?.details)
  return data as T
}
