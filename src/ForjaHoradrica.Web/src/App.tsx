import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { api, ApiError } from './api'
import { Icon, Stepper, Modal, tabListKeys } from './components/ui'
import InventoryEditor from './components/InventoryEditor'
import type { CatalogTarget } from './components/InventoryEditor'
import MercenaryEditor from './components/MercenaryEditor'
import CharacterWizard from './components/CharacterWizard'
import ItemWorkshop, { exportItemBlueprint } from './components/ItemWorkshop'
import WorldEditor from './components/WorldEditor'
import BuildLibrary from './components/BuildLibrary'
import { pageNames, slotName } from './components/items'
import type { Attributes, BackupEntry, Bootstrap, Catalog, CatalogEntry, Changes, CharacterSession, CommitResult, ExportResult, GearChange, GearType, Item, ItemOperation, LoadoutDocument, Preview, Profile, Skill, WorldChange } from './api'

type Tab = 'character' | 'skills' | 'equipment' | 'mercenary' | 'world' | 'workshop' | 'changes'
type AttributeKey = 'strength' | 'dexterity' | 'vitality' | 'energy'
type Draft = { name: string; level: number; attributes: Attributes; skills: Record<number, number> }
type EditorOptions = { skills: boolean; stats: boolean; anya: boolean; unlockHell: boolean; unlockWaypoints: boolean; fillTomes: boolean; fillBelt: boolean }
type EditorSnapshot = { draft: Draft | null; gear: GearChange[]; moveToStash: string[]; preset?: string; options: EditorOptions; itemOperations: ItemOperation[]; activeWeaponSet?: 1 | 2; mercenary?: Changes['mercenary']; world?: WorldChange; loadout?: LoadoutDocument; loadoutIncludeCharacter?: boolean; loadoutKeepPrevious?: boolean }
function compileChanges(session: CharacterSession | null, state: EditorSnapshot): Changes | null {
  if (!session || !state.draft) return null
  const { draft, gear, moveToStash, preset, options, itemOperations, activeWeaponSet, mercenary, world, loadout, loadoutIncludeCharacter, loadoutKeepPrevious } = state
  const current = session.character
  const attributes = Object.fromEntries((['strength', 'dexterity', 'vitality', 'energy', 'gold', 'stashGold'] as const).filter(key => draft.attributes[key] !== current.attributes[key]).map(key => [key, draft.attributes[key]]))
  const skills = session.skills.some(skill => draft.skills[skill.id] !== skill.points) ? session.skills.map(skill => ({ id: skill.id, points: draft.skills[skill.id] })) : []
  return { sessionId: session.sessionId, ...(draft.name !== current.name ? { name: draft.name } : {}), ...(draft.level !== current.level ? { level: draft.level } : {}), ...(Object.keys(attributes).length ? { attributes } : {}), ...(skills.length ? { skills } : {}), ...(gear.length ? { gear } : {}), ...(moveToStash.length ? { moveToStashItemIds: moveToStash } : {}), ...(preset ? { preset } : {}), ...(options.skills || options.stats || options.anya ? { questRewards: { skills: options.skills, stats: options.stats, anya: options.anya } } : {}), ...(options.unlockHell ? { unlockHell: true } : {}), ...(options.unlockWaypoints ? { unlockWaypoints: true } : {}), ...(options.fillTomes ? { fillTomes: true } : {}), ...(options.fillBelt ? { fillBelt: true } : {}), ...(itemOperations.length ? { itemOperations } : {}), ...(activeWeaponSet ? { activeWeaponSet } : {}), ...(mercenary ? { mercenary } : {}), ...(world ? { world } : {}), ...(loadout ? { loadout, loadoutIncludeCharacter, loadoutKeepPrevious } : {}) }
}
const ATTRIBUTES: { key: AttributeKey; label: string; initial: string }[] = [
  { key: 'strength', label: 'Fuerza', initial: 'F' }, { key: 'dexterity', label: 'Destreza', initial: 'D' },
  { key: 'vitality', label: 'Vitalidad', initial: 'V' }, { key: 'energy', label: 'Energía', initial: 'E' },
]
const CLASS_NAMES: Record<number, string> = { 0: 'Amazona', 1: 'Hechicera', 2: 'Nigromante', 3: 'Paladín', 4: 'Bárbaro', 5: 'Druida', 6: 'Asesina', 7: 'Brujo' }
const TREE_NAMES: Record<number, string[]> = { 0: ['Jabalinas y lanzas', 'Pasivas y magia', 'Arco y ballesta'], 1: ['Fuego', 'Relámpago', 'Frío'], 2: ['Maldiciones', 'Hueso y veneno', 'Invocación'], 3: ['Combate', 'Auras ofensivas', 'Auras defensivas'], 4: ['Combate', 'Dominio del combate', 'Gritos de guerra'], 5: ['Elementales', 'Cambio de forma', 'Invocación'], 6: ['Artes marciales', 'Disciplinas de las sombras', 'Trampas'] }
const SLOT_LIST = [
  { id: 'helm', label: 'Casco', position: 'head' }, { id: 'amulet', label: 'Amuleto', position: 'neck' },
  { id: 'weapon', label: 'Arma', position: 'weapon' }, { id: 'armor', label: 'Armadura', position: 'armor' },
  { id: 'shield', label: 'Escudo', position: 'shield' }, { id: 'gloves', label: 'Guantes', position: 'gloves' },
  { id: 'belt', label: 'Cinturón', position: 'belt' }, { id: 'boots', label: 'Botas', position: 'boots' },
  { id: 'ring1', label: 'Anillo derecho', position: 'ring-one' }, { id: 'ring2', label: 'Anillo izquierdo', position: 'ring-two' },
]
const QUALITY_NAMES: Record<string, string> = { unique: 'Único', set: 'Conjunto', rare: 'Raro', magic: 'Mágico', normal: 'Normal', superior: 'Superior', runeword: 'Palabra rúnica', base: 'Base' }
const QUALITY_TYPES: { value: GearType; label: string }[] = [{ value: 'unique', label: 'Únicos' }, { value: 'set', label: 'Conjuntos' }, { value: 'runeword', label: 'Palabras rúnicas' }, { value: 'base', label: 'Bases' }]
const number = new Intl.NumberFormat('es-ES')
const message = (error: unknown) => error instanceof Error ? error.message : 'Se ha producido un error inesperado.'
const draftOf = (session: CharacterSession): Draft => ({ name: session.character.name, level: session.character.level, attributes: { ...session.character.attributes }, skills: Object.fromEntries(session.skills.map(s => [s.id, s.points])) })
const SECTION_NAMES: Record<string, string> = { skills: 'Habilidades', items: 'Equipo', attributes: 'Atributos', stats: 'Atributos', character: 'Personaje', quests: 'Misiones', progression: 'Progreso', preset: 'Configuración', waypoints: 'Transportadores' }
function readable(value: unknown): string {
  if (value == null) return '—'
  if (Array.isArray(value)) return value.map(readable).join(', ')
  if (typeof value === 'object') {
    const entry = value as Record<string, unknown>
    if (entry.owner && entry.page) return [entry.owner === 'mercenary' ? 'Mercenario' : 'Personaje', entry.page === 'equipped' ? slotName(String(entry.slot || '')) : pageNames[String(entry.page)] || entry.page, entry.weaponSet ? `Equipo ${entry.weaponSet === 2 ? 'II' : 'I'}` : null].filter(Boolean).join(' · ')
    return entry.name ? String(entry.name) : Object.entries(entry).map(([key, content]) => `${SECTION_NAMES[key] || key}: ${readable(content)}`).join(' · ')
  }
  if (typeof value === 'boolean') return value ? 'Sí' : 'No'
  if (typeof value === 'number') return number.format(value)
  return String(value).replace(/\binventory\b/g, 'inventario').replace(/\bstash\b/g, 'baúl').replace(/\bunique\b/g, 'único').replace(/\bset\b/g, 'conjunto').replace(/\bweapon\b/g, 'arma').replace(/\bnotStarted\b/g, 'Pendiente').replace(/\bstarted\b/g, 'En curso').replace(/\bcompleted\b/g, 'Completada').replace(/\brewarded\b/g, 'Recompensa cobrada').replace(/orbe-tal-oculus/g, 'Orbe, Tal Rasha y Oculus')
}
function readableDiff(preview: Preview | null, source: CharacterSession | null) {
  if (!preview || !source) return []
  return preview.diff.flatMap(diff => {
    if (diff.section.toLowerCase() === 'skills') return preview.summary.skills.filter(s => s.points !== (source.skills.find(old => old.id === s.id)?.points || 0)).map(s => ({ section: 'Habilidades', label: s.name, before: String(source.skills.find(old => old.id === s.id)?.points || 0), after: String(s.points) }))
    return [{ section: SECTION_NAMES[diff.section.toLowerCase()] || diff.section, label: readable(diff.label), before: readable(diff.before), after: readable(diff.after) }]
  })
}

export default function App() {
  const [bootstrap, setBootstrap] = useState<Bootstrap | null>(null)
  const [closed, setClosed] = useState(false)
  const [session, setSession] = useState<CharacterSession | null>(null)
  const [draft, setDraft] = useState<Draft | null>(null)
  const [tab, setTab] = useState<Tab>('character')
  const [workshopVisited, setWorkshopVisited] = useState(false)
  const [workshopItem, setWorkshopItem] = useState<Item | null>(null)
  const clearWorkshopInitial = useCallback(() => setWorkshopItem(null), [])
  const editorScroller = useRef<HTMLDivElement>(null)
  const [profileId, setProfileId] = useState('')
  const profilesLoaded = useRef(false)
  const [busy, setBusy] = useState<string | null>('Cargando partidas…')
  const [error, setError] = useState<string | null>(null)
  const [toast, setToast] = useState<{ text: string; detail?: string; persistent?: boolean; exportedPath?: string } | null>(null)
  const [copiedPath, setCopiedPath] = useState(false)
  const [preview, setPreview] = useState<Preview | null>(null)
  const [gear, setGear] = useState<GearChange[]>([])
  const [moveToStash, setMoveToStash] = useState<string[]>([])
  const [preset, setPreset] = useState<string | undefined>()
  const [options, setOptions] = useState<EditorOptions>({ skills: false, stats: false, anya: false, unlockHell: false, unlockWaypoints: false, fillTomes: false, fillBelt: false })
  const [itemOperations, setItemOperations] = useState<ItemOperation[]>([])
  const [activeWeaponSet, setActiveWeaponSet] = useState<1 | 2 | undefined>()
  const [mercenaryChange, setMercenaryChange] = useState<Changes['mercenary']>()
  const [worldChange, setWorldChange] = useState<WorldChange>()
  const [loadout, setLoadout] = useState<LoadoutDocument>()
  const [loadoutIncludeCharacter, setLoadoutIncludeCharacter] = useState(true)
  const [loadoutKeepPrevious, setLoadoutKeepPrevious] = useState(true)
  const [libraryOpen, setLibraryOpen] = useState(false)
  const [undoStack, setUndoStack] = useState<EditorSnapshot[]>([])
  const [redoStack, setRedoStack] = useState<EditorSnapshot[]>([])
  const [reviewAcknowledged, setReviewAcknowledged] = useState(false)
  const [openDialog, setOpenDialog] = useState(false)
  const [wizardMode, setWizardMode] = useState<'new' | 'clone' | null>(null)
  const [newDestination, setNewDestination] = useState<string | null>(null)
  const [manualPath, setManualPath] = useState('')
  const [manualTables, setManualTables] = useState('')
  const [catalogTarget, setCatalogTarget] = useState<CatalogTarget | null>(null)
  const [backupDialog, setBackupDialog] = useState(false)
  const [backups, setBackups] = useState<BackupEntry[]>([])
  const [tree, setTree] = useState<number | null>(null)
  const [dirtyRevision, setDirtyRevision] = useState(0)
  const [reviewedRevision, setReviewedRevision] = useState(-1)
  const gameRunning = bootstrap?.gameRunning ?? session?.gameRunning ?? false
  function capture(): EditorSnapshot { return structuredClone({ draft, gear, moveToStash, preset, options, itemOperations, activeWeaponSet, mercenary: mercenaryChange, world: worldChange, loadout, loadoutIncludeCharacter, loadoutKeepPrevious }) }
  function changed() { setUndoStack(stack => [...stack, capture()].slice(-60)); setRedoStack([]); setDirtyRevision(n => n + 1); setReviewAcknowledged(false); setError(null); setToast(current => current?.persistent ? current : null) }
  function restoreSnapshot(snapshot: EditorSnapshot) { setDraft(snapshot.draft); setGear(snapshot.gear); setMoveToStash(snapshot.moveToStash); setPreset(snapshot.preset); setOptions(snapshot.options); setItemOperations(snapshot.itemOperations); setActiveWeaponSet(snapshot.activeWeaponSet); setMercenaryChange(snapshot.mercenary); setWorldChange(snapshot.world); setLoadout(snapshot.loadout); setLoadoutIncludeCharacter(snapshot.loadoutIncludeCharacter !== false); setLoadoutKeepPrevious(snapshot.loadoutKeepPrevious !== false) }
  const reset = useCallback((opened: CharacterSession) => {
    setSession(opened); setDraft(draftOf(opened)); setGear([]); setMoveToStash([]); setPreset(undefined); setPreview(null)
    setOptions({ skills: false, stats: false, anya: false, unlockHell: false, unlockWaypoints: false, fillTomes: false, fillBelt: false })
    setDirtyRevision(0); setReviewedRevision(-1); setTree(null); setError(null)
    setItemOperations([]); setActiveWeaponSet(undefined); setMercenaryChange(undefined); setUndoStack([]); setRedoStack([]); setReviewAcknowledged(false)
    setWorkshopVisited(false); setWorkshopItem(null)
    setWorldChange(undefined)
    setLoadout(undefined); setLoadoutIncludeCharacter(true); setLoadoutKeepPrevious(true); setLibraryOpen(false)
  }, [])
  const refresh = useCallback(async (silent = false) => {
    if (!silent) { setBusy('Buscando partidas…'); setError(null) }
    try { const value = await api<Bootstrap>('/bootstrap'); setBootstrap(value); if (!profilesLoaded.current) { setProfileId(value.defaultProfileId || value.profiles[0]?.id || ''); profilesLoaded.current = true } }
    catch (e) { if (!silent) setError(message(e)) }
    finally { if (!silent) setBusy(null) }
  }, [])
  useEffect(() => { if (closed) return; void refresh(); const timer = window.setInterval(() => { void refresh(true) }, 7000); return () => window.clearInterval(timer) }, [refresh, closed])
  useEffect(() => { editorScroller.current?.scrollTo({ top: 0 }) }, [tab, session?.sessionId])
  useEffect(() => { if (tab === 'workshop') setWorkshopVisited(true) }, [tab])
  useEffect(() => { if (!toast || toast.persistent) return; const timer = window.setTimeout(() => setToast(null), 12000); return () => window.clearTimeout(timer) }, [toast])

  async function openSave(path: string, selectedProfile?: string) {
    if (!path.trim()) return
    setBusy('Abriendo personaje…'); setError(null)
    try {
      let resolvedProfile = selectedProfile || profileId || undefined
      if (manualTables.trim()) {
        const profile = await api<Profile>('/profiles', { excelPath: manualTables.trim() })
        resolvedProfile = profile.id
        await refresh(true)
      }
      const value = await api<CharacterSession>('/open', { path: path.trim(), profileId: resolvedProfile })
      reset(value); setProfileId(value.profileId); setTab('character'); setOpenDialog(false); setManualPath(''); setManualTables('')
    }
    catch (e) { setError(message(e)) }
    finally { setBusy(null) }
  }
  function updateDraft(partial: Partial<Draft>) { if (!draft) return; setDraft({ ...draft, ...partial }); changed() }
  function updateAttribute(key: keyof Attributes, value: number) { if (!draft) return; updateDraft({ attributes: { ...draft.attributes, [key]: value } }) }
  function setSkill(id: number, value: number) { if (!draft) return; updateDraft({ skills: { ...draft.skills, [id]: value } }) }
  const changes = useMemo(() => compileChanges(session, { draft, gear, moveToStash, preset, options, itemOperations, activeWeaponSet, mercenary: mercenaryChange, world: worldChange, loadout, loadoutIncludeCharacter, loadoutKeepPrevious }), [session, draft, gear, moveToStash, preset, options, itemOperations, activeWeaponSet, mercenaryChange, worldChange, loadout, loadoutIncludeCharacter, loadoutKeepPrevious])
  const hasChanges = changes ? Object.keys(changes).length > 1 : false
  const budgetSource = preview?.canSave ? preview.summary : session
  const attributePoints = session && draft ? (draft.level - 1) * (session.progression?.statsPerLevel ?? 5) + (budgetSource?.questRewards?.statPoints || 0) - ATTRIBUTES.reduce((sum, attribute) => sum + draft.attributes[attribute.key] - (session.progression?.baseAttributes[attribute.key] || 0), 0) : 0
  const skillPoints = session && draft ? (draft.level - 1) * (session.progression?.skillsPerLevel ?? 1) + (budgetSource?.questRewards?.skillPoints || 0) - Object.values(draft.skills).reduce((sum, points) => sum + points, 0) : 0
  const previewCurrent = !!preview && dirtyRevision === reviewedRevision
  const diffRows = useMemo(() => readableDiff(preview, session), [preview, session])
  const canExport = !!preview?.canSave && previewCurrent && reviewAcknowledged && !busy
  const renamed = !!(previewCurrent && preview && session && !session.isNew && preview.summary.character.name !== session.character.name)
  const canSave = canExport && !gameRunning && !renamed
  const selectedProfile = bootstrap?.profiles.find(p => p.id === (session?.profileId || profileId))

  async function reviewChanges() {
    if (!changes) return
    setBusy('Comprobando cambios…'); setError(null)
    try { const result = await api<Preview>('/preview', changes); setPreview(result); setReviewedRevision(dirtyRevision); setReviewAcknowledged(true); setTab('changes') }
    catch (e) { setError(message(e)); setTab('changes') }
    finally { setBusy(null) }
  }
  async function applyInventoryChange(patch: Partial<EditorSnapshot>) {
    if (!session || !changes || busy) return
    const next = { ...capture(), ...patch }; const request = compileChanges(session, next)
    setBusy('Comprobando operación…'); setError(null)
    try {
      const result = await api<Preview>('/preview', request)
      if (!result.canSave) {
        const restored = await api<Preview>('/preview', changes)
        setPreview(restored); setReviewedRevision(dirtyRevision)
        throw new Error(result.validation.errors.join(' ') || 'La operación no es válida.')
      }
      if (patch.preset || patch.loadout) next.draft = draftOf(result.summary)
      changed(); restoreSnapshot(next); setPreview(result); setReviewedRevision(dirtyRevision + 1)
    } catch (reason) { setError(message(reason)); throw reason }
    finally { setBusy(null) }
  }
  async function undoRedo(direction: 'undo' | 'redo') {
    const stack = direction === 'undo' ? undoStack : redoStack
    const target = stack.at(-1)
    if (!target || busy || !session) return
    const current = capture(); restoreSnapshot(target); setDirtyRevision(revision => revision + 1); setReviewAcknowledged(false)
    if (direction === 'undo') { setUndoStack(stack.slice(0, -1)); setRedoStack(previous => [...previous, current]) }
    else { setRedoStack(stack.slice(0, -1)); setUndoStack(previous => [...previous, current]) }
    setBusy(direction === 'undo' ? 'Deshaciendo cambio…' : 'Rehaciendo cambio…'); setError(null)
    try { const result = await api<Preview>('/preview', compileChanges(session, target)); setPreview(result); setReviewedRevision(dirtyRevision + 1) }
    catch (reason) { setError(message(reason)); setPreview(null) }
    finally { setBusy(null) }
  }
  useEffect(() => {
    const handle = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null
      if (!(event.ctrlKey || event.metaKey) || event.altKey || target?.matches('input,textarea,[contenteditable="true"]')) return
      if (event.key.toLowerCase() === 'z') { event.preventDefault(); void undoRedo(event.shiftKey ? 'redo' : 'undo') }
      else if (event.key.toLowerCase() === 'y') { event.preventDefault(); void undoRedo('redo') }
    }
    window.addEventListener('keydown', handle)
    return () => window.removeEventListener('keydown', handle)
  }, [undoStack, redoStack, busy, session])
  async function saveChanges() {
    if (!canSave || !preview || !session) return
    if (session.isNew) {
      const directory = bootstrap?.saveDirectories?.find(entry => entry.profileId === session.profileId)?.path || bootstrap?.saves.find(save => save.profileId === session.profileId)?.path.replace(/[\\/][^\\/]+$/, '')
      setNewDestination(directory ? `${directory}\\${preview.summary.character.name}.d2s` : '')
      return
    }
    setBusy('Guardando y creando copia…'); setError(null)
    try {
      const result = await api<CommitResult>('/commit', { sessionId: session.sessionId, planId: preview.planId, expectedHash: session.sourceHash })
      reset(result.summary); setTab('character'); setToast({ text: 'Personaje guardado. Tu partida anterior tiene una copia de seguridad.', detail: result.backupPath }); void refresh(true)
    } catch (e) { setError(message(e)); if (e instanceof ApiError && e.code.toLowerCase().includes('game')) void refresh(true) }
    finally { setBusy(null) }
  }
  async function saveNewCharacter() {
    if (!canSave || !preview || !session || !newDestination?.trim()) return
    setBusy('Creando archivo del personaje…'); setError(null)
    try {
      const result = await api<CommitResult>('/commit', { sessionId: session.sessionId, planId: preview.planId, expectedHash: session.sourceHash, destinationPath: newDestination.trim() })
      reset(result.summary); setNewDestination(null); setTab('character'); setToast({ text: 'Personaje creado. El archivo ya está listo para abrirlo en el juego.', detail: result.outputPath, persistent: true }); void refresh(true)
    } catch (reason) { setError(message(reason)) }
    finally { setBusy(null) }
  }
  async function reloadCurrent() {
    if (!session) return
    if (!session.isNew) { await openSave(session.sourcePath || '', session.profileId); return }
    setBusy('Restableciendo el borrador…'); setError(null)
    try { reset(await api<CharacterSession>(`/sessions/${encodeURIComponent(session.sessionId)}`)) }
    catch (reason) { setError(message(reason)) }
    finally { setBusy(null) }
  }
  async function exportCopy() {
    if (!canExport || !preview || !session) return
    setBusy('Exportando copia…'); setError(null)
    try {
      const result = await api<ExportResult>('/export', { sessionId: session.sessionId, planId: preview.planId })
      setCopiedPath(false)
      setToast({ text: 'Copia exportada. La partida original se conserva.', detail: result.outputPath, exportedPath: result.outputPath, persistent: true })
    } catch (e) { setError(message(e)) }
    finally { setBusy(null) }
  }
  async function copyExportPath() {
    if (!toast?.exportedPath) return
    try { await navigator.clipboard.writeText(toast.exportedPath); setCopiedPath(true) }
    catch { setError('No se pudo copiar la ruta. Puedes seleccionarla directamente en el aviso de exportación.') }
  }
  async function closeApplication() {
    setBusy('Cerrando AKARA…'); setError(null)
    try { await api<{ closed: boolean }>('/shutdown', {}); setClosed(true) }
    catch (e) { setError(message(e)) }
    finally { setBusy(null) }
  }
  async function loadBackups() {
    if (!session) return
    setBackupDialog(true); setBusy('Buscando copias…')
    try { const result = await api<{ entries: BackupEntry[] }>(`/backups?sessionId=${encodeURIComponent(session.sessionId)}`); setBackups(result.entries) }
    catch (e) { setError(message(e)) }
    finally { setBusy(null) }
  }
  async function restoreBackup(backupId: string) {
    if (!session || gameRunning) return
    setBusy('Restaurando copia…'); setError(null)
    try { const result = await api<CommitResult>('/restore', { sessionId: session.sessionId, backupId, expectedHash: session.sourceHash }); reset(result.summary); setBackupDialog(false); setToast({ text: 'Copia restaurada. Se ha conservado también la versión anterior.', detail: result.backupPath }) }
    catch (e) { setError(message(e)) }
    finally { setBusy(null) }
  }
  async function addGear(item: GearChange) {
    const { slot, mercSlot, page, weaponSet, clientId, ...nativeGear } = item
    const destination = mercSlot ? { owner: 'mercenary' as const, page: 'equipped' as const, slot: mercSlot } : slot ? { owner: 'character' as const, page: 'equipped' as const, slot, weaponSet: weaponSet || 1 as const } : { owner: 'character' as const, page: (page || 'inventory') as 'inventory' | 'stash' | 'cube' }
    await applyInventoryChange({ itemOperations: [...itemOperations, { operation: 'create', clientId: clientId || `n${crypto.randomUUID().replaceAll('-', '')}`, destination, gear: nativeGear }] }); setCatalogTarget(null)
  }
  function openWorkshop(item: Item) { setWorkshopItem(item); setWorkshopVisited(true); setTab('workshop') }
  async function blueprintFor(item: Item) {
    if (!session) return
    setBusy('Exportando plano del objeto…'); setError(null)
    try { await exportItemBlueprint(session.sessionId, item.id, preview?.canSave ? preview.planId : undefined); setToast({ text: 'Plano de objeto exportado. Puedes importarlo en otro borrador del mismo perfil.' }) }
    catch (reason) { setError(message(reason)); throw reason }
    finally { setBusy(null) }
  }
  async function changeWorld(patch: WorldChange) {
    const quests = new Map((worldChange?.quests || []).map(quest => [`${quest.difficulty}:${quest.questId}`, quest]))
    const waypoints = new Map((worldChange?.waypoints || []).map(waypoint => [`${waypoint.difficulty}:${waypoint.id}`, waypoint]))
    for (const quest of patch.quests || []) quests.set(`${quest.difficulty}:${quest.questId}`, quest)
    for (const waypoint of patch.waypoints || []) waypoints.set(`${waypoint.difficulty}:${waypoint.id}`, waypoint)
    await applyInventoryChange({ world: { ...worldChange, ...patch, quests: [...quests.values()], waypoints: [...waypoints.values()] } })
  }
  async function prepareConfiguration(patch: { preset?: string; loadout?: LoadoutDocument; loadoutIncludeCharacter?: boolean; loadoutKeepPrevious?: boolean }) {
    if (!session || !draft) return
    await applyInventoryChange({ draft: { ...draftOf(session), name: draft.name }, gear: [], moveToStash: [], itemOperations: [], options: { skills: false, stats: false, anya: false, unlockHell: false, unlockWaypoints: false, fillTomes: false, fillBelt: false }, world: undefined, mercenary: undefined, activeWeaponSet: undefined, preset: undefined, loadout: undefined, ...patch })
    setTab('changes')
  }
  const displayed = previewCurrent ? preview?.summary : session
  const workingSession = preview?.canSave ? preview.summary : session
  const trees = useMemo(() => {
    if (!session) return []
    const groups = new Map<number, { id: number; name: string; skills: Skill[] }>()
    for (const skill of session.skills) {
      const id = skill.tabId ?? 0
      if (!groups.has(id)) groups.set(id, { id, name: skill.tab || TREE_NAMES[session.character.classId]?.[Math.max(0, id - 1)] || `Árbol ${id || 1}`, skills: [] })
      groups.get(id)!.skills.push(skill)
    }
    return [...groups.values()].sort((a, b) => a.id - b.id)
  }, [session])
  const currentTree = trees.find(t => t.id === tree) || trees[0]
  const characterClass = session ? CLASS_NAMES[session.character.classId] || session.character.className : ''
  const preferredPreset = bootstrap?.presets.find(entry => entry.id === 'squad-sorceress' && entry.className === session?.character.className) || bootstrap?.presets.find(entry => entry.className === session?.character.className)

  if (closed) return <main className="closed-screen" role="status"><div className="welcome-monogram" aria-hidden="true">A</div><p className="eyebrow">AKARA</p><h1>AKARA se ha cerrado.</h1><p>Puedes cerrar esta pestaña.</p></main>

  return <div className="app-shell">
    <aside className="sidebar">
      <div className="brand"><div className="brand-mark" aria-hidden="true">A</div><div><span className="brand-name">AKARA</span><span className="brand-caption">EDITOR DE PERSONAJES</span></div></div>
      <div className="sidebar-section"><div className="section-label"><span>PARTIDAS LOCALES</span><button className="icon-button" title="Buscar partidas de nuevo" aria-label="Actualizar partidas" onClick={() => void refresh()} disabled={!!busy}><Icon name="refresh" size={15} /></button></div>
        <label className="sr-only" htmlFor="profile">Juego o mod</label><select id="profile" value={profileId} onChange={e => setProfileId(e.target.value)}><option value="">Todos los perfiles</option>{bootstrap?.profiles.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select>
      </div>
      <nav className="character-list" aria-label="Personajes guardados">
        {(bootstrap?.saves || []).filter(s => !profileId || s.profileId === profileId).map(save => <button key={save.path} className={`character-button ${session?.sourcePath?.toLowerCase() === save.path.toLowerCase() ? 'selected' : ''}`} onClick={() => void openSave(save.path, save.profileId)} disabled={!!busy}>
          <span className="character-seal" aria-hidden="true">{save.name.slice(0, 1).toUpperCase()}</span><span><strong>{save.name}</strong><small>{bootstrap?.profiles.find(p => p.id === save.profileId)?.name || 'Partida local'}</small></span><span className="character-chevron">›</span>
        </button>)}
        {bootstrap && !bootstrap.saves.some(s => !profileId || s.profileId === profileId) && <p className="sidebar-empty">No se han encontrado personajes en este perfil.</p>}
      </nav>
      <div className="sidebar-bottom"><button className="button subtle full new-character-button" disabled={!!busy || !bootstrap?.profiles.length} onClick={() => setWizardMode('new')}>Nuevo personaje</button><button className="button subtle full" onClick={() => setOpenDialog(true)}><Icon name="folder" />Abrir otra partida</button>{session && !session.isNew && <button className="text-button full" onClick={() => void loadBackups()}>Copias de seguridad</button>}<div className="local-note"><span className="status-dot" /> Solo en este equipo <small>{bootstrap?.version ? `AKARA ${bootstrap.version}` : 'Tu taller, tus personajes'}</small></div><button className="quit-button" disabled={!!busy} onClick={() => void closeApplication()} title="Cerrar la aplicación local. Los cambios pendientes no se guardan.">Salir de AKARA</button></div>
    </aside>

    <div className="workspace">
      <header className="topbar"><span className="topbar-label">DIABLO II <span> / </span> {selectedProfile?.name || 'PERSONAJES OFFLINE'}</span><span className={`game-status ${gameRunning ? 'running' : ''}`}><span className="status-dot" />{gameRunning ? 'Diablo II está abierto' : bootstrap ? 'Juego cerrado' : 'Conectando…'}</span></header>
      {error && <div role="alert" className="notice error"><span className="notice-symbol">!</span><div><strong>No se pudo completar la operación</strong><p>{error}</p></div><button className="icon-button" aria-label="Cerrar aviso" onClick={() => setError(null)}><Icon name="close" size={16} /></button></div>}
      {toast && <div role="status" className="notice success"><Icon name="check" /><div><strong>{toast.text}</strong>{toast.detail && <p className="path-text">{toast.detail}</p>}{toast.exportedPath && <div className="notice-actions"><button disabled={!!busy} onClick={() => void copyExportPath()}>{copiedPath ? 'Ruta copiada' : 'Copiar ruta'}</button><button disabled={!!busy} onClick={() => void openSave(toast.exportedPath!, session?.profileId)}>Abrir copia</button></div>}</div><button className="icon-button" aria-label="Cerrar aviso" onClick={() => setToast(null)}><Icon name="close" size={16} /></button></div>}
      {!session || !draft ? <main className="welcome">
        <div className="welcome-monogram" aria-hidden="true">A</div><p className="eyebrow">TU PERSONAJE. TU PRÓXIMA HISTORIA.</p><h1>Todo empieza<br />con una buena partida.</h1><p>Abre un personaje, ajusta su equipo y revisa los cambios antes de volver a Santuario.</p><button className="button primary" disabled={!!busy} onClick={() => setOpenDialog(true)}><Icon name="folder" />Abrir personaje</button><div className="welcome-footnote"><Icon name="shield" size={16} />Cada guardado conserva una copia de la partida anterior.</div>
        {busy && <p className="loading-line" role="status"><span className="spinner" />{busy}</p>}
      </main> : <>
        <div className="character-header"><div><div className="character-kicker"><span>{characterClass}</span><span className="kicker-dot">·</span><span>Nivel {draft.level}</span><span className="offline-label">OFFLINE</span></div><h1>{session.character.name}<span className="header-rule" /></h1><p className="source-path" title={session.sourcePath || undefined}>{session.isNew ? 'Nuevo personaje · Sin guardar' : session.sourcePath}</p></div><div className="header-actions"><button className="text-button" disabled={!!busy} onClick={() => setWizardMode('clone')}>Clonar personaje</button><div className="history-actions"><button disabled={!!busy || !undoStack.length} aria-label="Deshacer" title="Deshacer · Ctrl+Z" onClick={() => void undoRedo('undo')}>↶<span>Deshacer</span></button><button disabled={!!busy || !redoStack.length} aria-label="Rehacer" title="Rehacer · Ctrl+Mayús+Z" onClick={() => void undoRedo('redo')}>↷<span>Rehacer</span></button></div><button className="button subtle compact" disabled={!!busy} onClick={() => void reloadCurrent()}><Icon name="refresh" size={16} />Volver a leer</button></div></div>
        <div className="tabbar" role="tablist" onKeyDown={tabListKeys} aria-label="Secciones del editor">{([{ id: 'character', label: 'Personaje' }, { id: 'skills', label: 'Habilidades' }, { id: 'equipment', label: 'Equipo' }, { id: 'mercenary', label: 'Mercenario' }, { id: 'world', label: 'Mundo' }, { id: 'workshop', label: 'Taller' }, { id: 'changes', label: 'Cambios' }] as const).map(t => <button key={t.id} role="tab" id={`tab-${t.id}`} aria-controls={tab === t.id ? 'editor-panel' : undefined} tabIndex={tab === t.id ? 0 : -1} aria-selected={tab === t.id} className={tab === t.id ? 'active' : ''} onClick={() => setTab(t.id)}>{t.label}{t.id === 'changes' && hasChanges && <span className="tab-count">{previewCurrent ? diffRows.length : '·'}</span>}</button>)}</div>
        <div className="editor-layout" ref={editorScroller}><fieldset className="editor-lock" disabled={!!busy}>
          <main className="editor-content" role="tabpanel" id="editor-panel" aria-labelledby={`tab-${tab}`}>
            {tab === 'character' && <>
              <div className="section-heading"><div><h2>Nivel y atributos</h2></div><span className={`budget-pill ${attributePoints < 0 ? 'invalid' : ''}`}><strong>{number.format(attributePoints)}</strong> puntos disponibles</span></div>
              <div className="identity-card"><label>Nombre<input value={draft.name} maxLength={15} onChange={e => updateDraft({ name: e.target.value })} /></label><label>Nivel<Stepper label="Nivel del personaje" value={draft.level} min={1} max={session.progression?.maxLevel ?? 99} onChange={level => updateDraft({ level })} /></label><div className="experience-label"><span>EXPERIENCIA ACTUAL</span><strong>{number.format(session.character.experience)}</strong><small>Se ajusta al cambiar de nivel.</small></div></div>
              <div className="attribute-grid">{ATTRIBUTES.map(a => <section className={`attribute-card ${draft.attributes[a.key] !== session.character.attributes[a.key] ? 'modified' : ''}`} key={a.key}><div className="attribute-heading"><span className="attribute-letter">{a.initial}</span><h3>{a.label}</h3></div><Stepper label={a.label} value={draft.attributes[a.key]} min={session.progression?.baseAttributes[a.key] ?? 0} onChange={value => updateAttribute(a.key, value)} /><p>{draft.attributes[a.key] !== session.character.attributes[a.key] ? `Antes: ${session.character.attributes[a.key]}` : 'Atributo base'}</p></section>)}</div>
              <div className="section-heading lower"><div><h2>Oro y consumibles</h2></div></div>
              <div className="provisions-grid"><label className="field-card"><span>Oro en el personaje</span><input type="number" min={0} max={2500000} value={draft.attributes.gold} onChange={e => updateAttribute('gold', Math.max(0, Number(e.target.value) || 0))} /><small>Disponible para mercaderes y reparaciones.</small></label><label className="field-card"><span>Oro en el baúl personal</span><input type="number" min={0} max={2500000} value={draft.attributes.stashGold} onChange={e => updateAttribute('stashGold', Math.max(0, Number(e.target.value) || 0))} /><small>El baúl compartido conserva su propio oro.</small></label></div>
              <div className="check-row"><label><input type="checkbox" checked={options.fillTomes} onChange={e => { setOptions({ ...options, fillTomes: e.target.checked }); changed() }} /><span>Rellenar los tomos</span></label><label><input type="checkbox" checked={options.fillBelt} onChange={e => { setOptions({ ...options, fillBelt: e.target.checked }); changed() }} /><span>Rejuvenecimientos en el cinturón</span></label></div>
              <details className="progression"><summary>Progreso y recompensas de misiones<span>+</span></summary><p>Se revisan las recompensas nativas antes de añadir puntos. Los cambios se aplican al guardar.</p><div className="progression-options">{([{ key: 'skills', label: 'Recompensas de habilidades' }, { key: 'stats', label: 'Recompensas de atributos' }, { key: 'anya', label: 'Resistencia de Anya' }, { key: 'unlockHell', label: 'Acceso a Infierno' }, { key: 'unlockWaypoints', label: 'Todos los transportadores' }] as const).map(o => <label key={o.key}><input type="checkbox" checked={options[o.key]} onChange={e => { setOptions({ ...options, [o.key]: e.target.checked }); changed() }} />{o.label}</label>)}</div></details>
            </>}

            {tab === 'skills' && <>
              <div className="section-heading"><div><p className="eyebrow">TU FORMA DE LUCHAR</p><h2>Habilidades de {characterClass.toLowerCase()}</h2></div><span className={`budget-pill ${skillPoints < 0 ? 'invalid' : ''}`}><strong>{skillPoints}</strong> puntos disponibles</span></div><p className="section-description">Asigna puntos base. Los objetos y los encantamientos añaden sus bonificaciones dentro del juego.</p>
              <div className="tree-tabs" role="tablist" onKeyDown={tabListKeys} aria-label="Árboles de habilidades">{trees.map(t => <button role="tab" aria-selected={currentTree?.id === t.id} className={currentTree?.id === t.id ? 'active' : ''} key={t.id} onClick={() => setTree(t.id)}>{t.name}<span>{t.skills.reduce((n, s) => n + draft.skills[s.id], 0)}</span></button>)}</div>
              <div className={`skill-tree ${currentTree?.skills.every(s => s.row && s.column) ? 'positioned' : ''}`}>
                {currentTree?.skills.map((s, index) => {
                  const points = draft.skills[s.id] || 0
                  const max = Math.max(0, Math.min(s.maxPoints, draft.level - s.requiredLevel + 1))
                  const unlocked = draft.level >= s.requiredLevel
                  return <section key={s.id} className={`skill-card ${points ? 'learned' : ''} ${points !== s.points ? 'modified' : ''} ${!unlocked ? 'locked' : ''}`} style={s.row && s.column ? { gridRow: s.row, gridColumn: s.column } : undefined}>
                    <div className="skill-top"><span className="skill-index">{String(index + 1).padStart(2, '0')}</span><span>NIV. {s.requiredLevel}</span></div><h3>{s.name}</h3><div className="skill-prerequisite">{s.prerequisites.length ? `Requiere: ${s.prerequisites.map(id => session.skills.find(v => v.id === id)?.name || `#${id}`).join(', ')}` : 'Sin habilidades previas'}</div><div className="skill-points"><button aria-label={`Reducir ${s.name}`} disabled={points <= 0} onClick={() => setSkill(s.id, points - 1)}>−</button><span><strong>{points}</strong> / {s.maxPoints}</span><button aria-label={`Aumentar ${s.name}`} disabled={!unlocked || points >= max} onClick={() => setSkill(s.id, points + 1)}>+</button></div>
                  </section>
                })}
              </div>
            </>}

            {tab === 'equipment' && <InventoryEditor session={workingSession!} busy={!!busy} onOperation={operation => applyInventoryChange({ itemOperations: [...itemOperations, operation] })} onCatalog={setCatalogTarget} onWeaponSet={set => applyInventoryChange({ activeWeaponSet: set })} onWorkshop={openWorkshop} onExportBlueprint={blueprintFor} />}
            {tab === 'mercenary' && <MercenaryEditor session={workingSession!} busy={!!busy} onChange={change => applyInventoryChange({ mercenary: { ...mercenaryChange, ...change } })} onOperation={operation => applyInventoryChange({ itemOperations: [...itemOperations, operation] })} onCatalog={setCatalogTarget} onWorkshop={openWorkshop} onExportBlueprint={blueprintFor} />}
            {tab === 'world' && <WorldEditor sessionId={session.sessionId} planId={preview?.canSave ? preview.planId : undefined} busy={!!busy} onChange={changeWorld} />}
            {workshopVisited && <div hidden={tab !== 'workshop'}><ItemWorkshop session={workingSession!} busy={!!busy} onOperation={operation => applyInventoryChange({ itemOperations: [...itemOperations, operation] })} initialItem={workshopItem} onClearInitial={clearWorkshopInitial} planId={preview?.canSave ? preview.planId : undefined} /></div>}
            {tab === 'changes' && <>
              <div className="section-heading"><div><p className="eyebrow">ANTES DE VOLVER A SANTUARIO</p><h2>Revisa tu próxima partida</h2></div>{previewCurrent && preview?.canSave && <span className="verified-label"><Icon name="check" size={16} />Revisión válida</span>}</div>
              {preset && <div className="preset-note"><div><p className="eyebrow">CONFIGURACIÓN SELECCIONADA</p><h3>{bootstrap?.presets.find(entry => entry.id === preset)?.name || 'Plantilla seleccionada'}</h3><p>El equipo y las habilidades se calcularán con las tablas del mod al revisar.</p></div><button className="text-button" onClick={() => { setPreset(undefined); changed() }}>Quitar</button></div>}
              {!preview ? <div className="review-empty"><div className="review-symbol"><Icon name="shield" size={30} /></div><h3>{hasChanges ? 'Los cambios están preparados.' : 'Tu partida sigue como la abriste.'}</h3><p>{hasChanges ? 'Revisa los puntos, los objetos y las misiones antes de guardar. Verás aquí cada diferencia.' : 'Ajusta el personaje o elige una configuración para empezar.'}</p>{hasChanges && <button className="button primary" disabled={!!busy} onClick={() => void reviewChanges()}>Revisar cambios<Icon name="arrow" /></button>}</div> : <>
                {!previewCurrent && <div className="notice warning"><span className="notice-symbol">!</span><p>Has cambiado algo después de la revisión. Revisa de nuevo antes de guardar.</p></div>}
                {preview.validation.errors.map((e, i) => <div role="alert" className="notice error" key={i}><span className="notice-symbol">!</span><p>{e}</p></div>)}
                {preview.validation.warnings.map((w, i) => <div className="notice warning" key={i}><span className="notice-symbol">!</span><p>{w}</p></div>)}
                <div className="diff-table"><div className="diff-row diff-head"><span>Cambio</span><span>Ahora</span><span>Al guardar</span></div>{diffRows.map((d, i) => <div className="diff-row" key={i}><span><small>{d.section}</small><strong>{d.label}</strong></span><span className="diff-before">{d.before}</span><span className="diff-after">{d.after}</span></div>)}{!diffRows.length && <p className="empty-inline">No hay diferencias con la partida abierta.</p>}</div>
                <div className="backup-assurance"><Icon name="shield" /><p><strong>Tu partida anterior queda a salvo.</strong><span>AKARA crea una copia antes de guardar y comprueba que el archivo se puede leer.</span></p></div>
              </>}
            </>}
          </main>

          <aside className="inspector"><div className="inspector-title"><span className="eyebrow">{previewCurrent ? 'AL GUARDAR' : 'PARTIDA ABIERTA'}</span><span className="tiny-rule" /></div><h3>{displayed?.character.name}</h3><p className="inspector-class">{characterClass} · Nivel {displayed?.character.level}</p><div className="vitals"><Vital label="Vida base" max={displayed?.character.vitals.maxLife || 0} kind="life" /><Vital label="Maná base" max={displayed?.character.vitals.maxMana || 0} kind="mana" /></div><dl className="summary-stats">{ATTRIBUTES.map(a => <div key={a.key}><dt>{a.label}</dt><dd>{number.format(displayed?.character.attributes[a.key] || 0)}</dd></div>)}</dl><p className="base-stats-note">Atributos base, antes de las bonificaciones del equipo.</p><div className="inspector-divider" />
            <div className={`file-check ${session.validation.canEdit ? '' : 'invalid'}`}><Icon name="shield" size={20} /><div><strong>{session.validation.canEdit ? 'Archivo verificado' : 'Edición no disponible'}</strong><span>{session.validation.checksum && session.validation.roundtrip ? 'Integridad y lectura correctas' : 'Consulta los detalles al revisar'}</span></div></div>
            {preferredPreset && <div className="preset-card"><p className="eyebrow">PLANTILLAS NATIVAS</p><div className="preset-emblem" aria-hidden="true"><span /><span /><span /></div><h4>{preferredPreset.name}</h4><p>Consulta la construcción de esta clase o guarda tu propio equipo para volver a usarlo.</p><button className="button subtle full" disabled={!!busy} onClick={() => setLibraryOpen(true)}>Ver plantillas y equipos<Icon name="arrow" size={16} /></button></div>}
          </aside>
          </fieldset>
        </div>
        <footer className="savebar"><div className="save-state">{busy ? <><span className="spinner" /><span>{busy}</span></> : renamed ? <><span className="status-dot amber" /><span>El nombre ha cambiado: exporta una copia.<small>El archivo original conserva su nombre.</small></span></> : gameRunning ? <><span className="status-dot amber" /><span>Cierra Diablo II para guardar.<small>Puedes exportar una copia mientras juegas.</small></span></> : previewCurrent && preview?.canSave && reviewAcknowledged ? <><span className="status-dot" /><span>Todo preparado para guardar.<small>Se creará una copia de seguridad.</small></span></> : <><span className={`status-dot ${hasChanges ? 'amber' : 'neutral'}`} /><span>{hasChanges ? 'Hay cambios pendientes de revisar.' : 'Sin cambios pendientes.'}<small>La partida se conserva hasta que guardes.</small></span></>}</div><div className="save-actions"><button className="button subtle" disabled={!!busy || !session.validation.canEdit} onClick={() => void reviewChanges()}>{preview && !previewCurrent ? 'Revisar de nuevo' : 'Revisar cambios'}</button><button className="button subtle" disabled={!canExport} onClick={() => void exportCopy()}>Exportar copia</button><button className="button primary" disabled={!canSave} onClick={() => void saveChanges()}><Icon name="save" size={17} />Guardar con copia</button></div></footer>
      </>}
    </div>

    {openDialog && <Modal title="Abrir un personaje" subtitle="Elige una partida local de Diablo II Resurrected." onClose={() => setOpenDialog(false)}><form className="open-form" onSubmit={e => { e.preventDefault(); void openSave(manualPath) }}>{error && <div role="alert" className="notice error"><p>{error}</p></div>}<label>Archivo del personaje<input autoComplete="off" autoFocus placeholder="C:\…\Saved Games\Diablo II Resurrected\…\Personaje.d2s" value={manualPath} onChange={e => setManualPath(e.target.value)} required /></label><label>Juego o mod<select value={profileId} onChange={e => setProfileId(e.target.value)}><option value="">Detectar desde la partida</option>{bootstrap?.profiles.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label><details><summary>Indicar la carpeta del mod</summary><label>Carpeta de tablas del juego<input value={manualTables} onChange={e => setManualTables(e.target.value)} placeholder="…\data\global\excel" /><small>Solo hace falta si AKARA no encuentra el mod instalado.</small></label></details><div className="modal-actions"><button type="button" className="button subtle" onClick={() => setOpenDialog(false)}>Cancelar</button><button className="button primary" disabled={!!busy || !manualPath.trim()} type="submit">Abrir personaje<Icon name="arrow" /></button></div></form></Modal>}
    {libraryOpen && session && <BuildLibrary session={workingSession!} canCapture={!hasChanges || !!preview?.canSave && previewCurrent} planId={previewCurrent && preview?.canSave ? preview.planId : undefined} busy={!!busy} onPreset={id => prepareConfiguration({ preset: id })} onLoadout={(document, includeCharacter, keepPrevious) => prepareConfiguration({ loadout: document, loadoutIncludeCharacter: includeCharacter, loadoutKeepPrevious: keepPrevious })} onClose={() => setLibraryOpen(false)} />}
    {wizardMode && bootstrap && <CharacterWizard bootstrap={bootstrap} current={session} preview={previewCurrent ? preview : null} initialMode={wizardMode} onClose={() => setWizardMode(null)} onReady={created => { reset(created); setProfileId(created.profileId); setTab('character'); setWizardMode(null); setToast({ text: 'Personaje preparado. Revisa el borrador antes de guardarlo.', persistent: true }) }} />}
    {newDestination !== null && session && <Modal title="Guardar el nuevo personaje" subtitle="Se creará un archivo nuevo. Un personaje existente nunca se sobrescribe." onClose={() => setNewDestination(null)}><form className="open-form" onSubmit={event => { event.preventDefault(); void saveNewCharacter() }}>{error && <div role="alert" className="notice error"><p>{error}</p></div>}<label>Archivo de destino<input value={newDestination} disabled={!!busy} onChange={event => setNewDestination(event.target.value)} placeholder={`C:\\…\\${preview?.summary.character.name || session.character.name}.d2s`} required /><small>El nombre del archivo debe coincidir con el nombre del personaje.</small></label><div className="modal-actions"><button type="button" className="button subtle" disabled={!!busy} onClick={() => setNewDestination(null)}>Cancelar</button><button type="submit" className="button primary" disabled={!canSave || !newDestination.trim()}>Crear archivo</button></div></form></Modal>}
    {catalogTarget && session && <CatalogDialog session={workingSession!} targetContext={catalogTarget} busy={!!busy} onClose={() => setCatalogTarget(null)} onAdd={addGear} />}
    {backupDialog && <Modal title="Copias de seguridad" subtitle="Cada guardado conserva la versión anterior del personaje." onClose={() => setBackupDialog(false)}><div className="backup-list">{backups.map(b => <div className="backup-entry" key={b.id}><div><strong>{new Date(b.createdAt).toLocaleString('es-ES')}</strong><p className="path-text">{b.path}</p></div><button className="button subtle compact" disabled={gameRunning || !!busy} onClick={() => void restoreBackup(b.id)}>Restaurar</button></div>)}{!backups.length && <p className="empty-inline">Todavía no hay copias de seguridad de este personaje.</p>}{gameRunning && <p className="small amber-text">Cierra Diablo II antes de restaurar una copia.</p>}</div></Modal>}
  </div>
}

function Vital({ label, max, kind }: { label: string; max: number; kind: 'life' | 'mana' }) {
  return <div className={`vital ${kind}`}><div><span>{label}</span><strong>{number.format(max)}</strong></div><div className="vital-track"><span style={{ width: '100%' }} /></div><small>Antes de las bonificaciones del equipo</small></div>
}
function CatalogDialog({ session, targetContext, busy, onClose, onAdd }: { session: CharacterSession; targetContext: CatalogTarget; busy: boolean; onClose: () => void; onAdd: (item: GearChange) => Promise<void> }) {
  const [type, setType] = useState<GearType>('unique')
  const [search, setSearch] = useState('')
  const [result, setResult] = useState<Catalog | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [chosen, setChosen] = useState<CatalogEntry | null>(null)
  const [baseCode, setBaseCode] = useState('')
  const [target, setTarget] = useState(targetContext.slot || targetContext.page || 'inventory')
  const [ethereal, setEthereal] = useState(false)
  const [owner, setOwner] = useState(targetContext.owner)
  const [bases, setBases] = useState<CatalogEntry[]>([])
  async function addSelected() {
    if (!chosen) return
    try { await onAdd({ type, name: chosen.name, ...(type === 'base' ? { baseCode: chosen.code || chosen.id } : {}), ...(type === 'runeword' ? { baseCode } : {}), ethereal, ...(owner === 'mercenary' ? { mercSlot: target } : target === 'inventory' || target === 'stash' || target === 'cube' ? { page: target } : { slot: target, weaponSet: targetContext.weaponSet || 1 }) }) }
    catch (reason) { setError(message(reason)) }
  }
  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    const timer = window.setTimeout(() => { api<Catalog>(`/catalog?sessionId=${encodeURIComponent(session.sessionId)}&type=${type}&search=${encodeURIComponent(search)}`, undefined, controller.signal).then(setResult).catch(e => { if (e.name !== 'AbortError') setError(message(e)) }).finally(() => { if (!controller.signal.aborted) setLoading(false) }) }, 180)
    return () => { window.clearTimeout(timer); controller.abort() }
  }, [session.sessionId, search, type])
  useEffect(() => {
    if (type !== 'runeword') return
    let cancelled = false
    void (async () => {
      const entries: CatalogEntry[] = []
      let total = Infinity
      while (entries.length < total) {
        const result = await api<Catalog>(`/catalog?sessionId=${encodeURIComponent(session.sessionId)}&type=base&search=&offset=${entries.length}&limit=500`)
        total = result.total
        if (!result.entries.length) break
        entries.push(...result.entries)
      }
      if (!cancelled) setBases(entries)
    })().catch(e => { if (!cancelled) setError(message(e)) })
    return () => { cancelled = true }
  }, [type, session.sessionId])
  return <Modal title="Catálogo de objetos" subtitle="Objetos nativos del juego o mod de este personaje." onClose={onClose} wide><div className="catalog-tools"><div className="search-field"><Icon name="search" /><input aria-label="Buscar un objeto" autoFocus value={search} onChange={e => setSearch(e.target.value)} placeholder="Busca por nombre: Oculus, Tal Rasha…" /></div><div className="catalog-types">{QUALITY_TYPES.map(t => <button className={type === t.value ? 'active' : ''} key={t.value} onClick={() => { setType(t.value); setChosen(null); setBaseCode(''); setError(null) }}>{t.label}</button>)}</div></div>
    <div className="catalog-body"><div className="catalog-results">{error && <div className="notice error"><p>{error}</p></div>}{loading && <p className="loading-line"><span className="spinner" />Buscando objetos…</p>}{!loading && result?.entries.map(item => <button disabled={!item.supported} className={`catalog-entry ${chosen?.id === item.id ? 'selected' : ''}`} key={item.id} onClick={() => setChosen(item)}><span className={`catalog-glyph quality-${type}`} aria-hidden="true">{(item.code || item.name).slice(0, 3).toUpperCase()}</span><span><strong className={`quality-${type}`}>{item.name}</strong><small>{item.supported ? [item.code?.toUpperCase(), item.levelRequired ? `Nivel ${item.levelRequired}` : undefined].filter(Boolean).join(' · ') : 'Todavía no disponible para edición'}</small></span><span className="entry-chevron">›</span></button>)}{!loading && !result?.entries.length && <p className="empty-inline">No hay objetos que coincidan con la búsqueda.</p>}</div><aside className="catalog-selection">{chosen ? <><p className="eyebrow">OBJETO SELECCIONADO</p><h3 className={`quality-${type}`}>{chosen.name}</h3><p className="small muted">{QUALITY_NAMES[type]}{chosen.levelRequired ? ` · Nivel ${chosen.levelRequired}` : ''}</p>{chosen.description && <p>{chosen.description}</p>}<label>Para<select value={owner} disabled={busy} onChange={event => { setOwner(event.target.value as CatalogTarget['owner']); setTarget(event.target.value === 'mercenary' ? 'weapon' : 'inventory') }}><option value="character">Personaje</option><option value="mercenary" disabled={!session.mercenary?.exists}>Mercenario</option></select></label><label>Destino<select value={target} disabled={busy} onChange={event => setTarget(event.target.value)}>{owner === 'character' && <><option value="inventory">Inventario</option><option value="stash">Baúl personal</option><option value="cube">Cubo horádrico</option></>}{SLOT_LIST.filter(slot => owner === 'character' || !session.mercenary?.allowedSlots || session.mercenary.allowedSlots.includes(slot.id)).map(slot => <option key={slot.id} value={slot.id}>{slot.label}</option>)}</select></label><label className="inline-check"><input type="checkbox" checked={ethereal} disabled={busy} onChange={event => setEthereal(event.target.checked)} />Objeto etéreo</label>{type === 'runeword' && <label>Objeto base<select value={baseCode} onChange={e => setBaseCode(e.target.value)}><option value="">Elige una base compatible</option>{bases.filter(b => b.supported).map(b => <option value={b.code || b.id} key={b.id}>{b.name} ({b.code})</option>)}</select><small>La revisión verifica la base y las runas.</small></label>}<div className="catalog-explanation"><Icon name="shield" size={17} /><p>Se usarán las propiedades nativas. Si sustituyes equipo, el objeto anterior irá al baúl.</p></div><button className="button primary full" disabled={busy || type === 'runeword' && !baseCode} onClick={() => void addSelected()}>Añadir a los cambios<Icon name="arrow" size={17} /></button></> : <div className="selection-empty"><Icon name="search" size={25} /><h3>Encuentra tu próxima pieza.</h3><p>Selecciona un objeto para elegir dónde colocarlo.</p></div>}</aside></div><div className="catalog-footer"><span>{result ? `${number.format(result.total)} objetos en esta búsqueda` : 'Tablas del perfil actual'}</span><button className="button subtle compact" onClick={onClose}>Volver al equipo</button></div>
  </Modal>
}
