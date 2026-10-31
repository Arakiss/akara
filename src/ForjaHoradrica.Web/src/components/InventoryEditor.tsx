import { useEffect, useMemo, useState } from 'react'
import type { CSSProperties } from 'react'
import type { CharacterSession, Item, ItemDestination, ItemOperation } from '../api'
import { Icon, Modal, tabListKeys } from './ui'
import { isWorn, itemAbbreviation, itemName, itemOwner, itemQuality, pageNames, placementName, qualities, slots } from './items'

export interface CatalogTarget { owner: 'character' | 'mercenary'; slot?: string; page?: string; weaponSet?: 1 | 2 }
export interface InventoryEditorProps {
  session: CharacterSession; busy: boolean; onOperation: (operation: ItemOperation) => Promise<void>;
  onCatalog: (target: CatalogTarget) => void; onWeaponSet: (set: 1 | 2) => Promise<void>;
  onWorkshop?: (item: Item) => void
  onExportBlueprint?: (item: Item) => Promise<void>
}

export default function InventoryEditor({ session, busy, onOperation, onCatalog, onWeaponSet, onWorkshop, onExportBlueprint }: InventoryEditorProps) {
  const [weaponSet, setWeaponSet] = useState<1 | 2>(session.activeWeaponSet || 1)
  const [page, setPage] = useState<'inventory' | 'stash' | 'cube' | 'belt'>('inventory')
  const [view, setView] = useState<'grid' | 'list'>('grid')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [filter, setFilter] = useState('')
  const selected = session.items.find(item => item.id === selectedId)
  const pageItems = session.items.filter(item => !isWorn(item) && item.page === page)
  const visibleItems = useMemo(() => pageItems.filter(item => `${itemName(item)} ${qualities[itemQuality(item)] || item.quality}`.toLowerCase().includes(filter.toLowerCase())), [pageItems, filter])
  const grid = page === 'stash' ? session.stashGrid : page === 'cube' ? session.cubeGrid : page === 'belt' ? session.beltGrid || { width: 4, height: 4 } : session.inventoryGrid
  const actualGrid = grid || { width: Math.max(page === 'stash' ? 10 : 4, ...pageItems.map(i => i.x + i.width)), height: Math.max(4, ...pageItems.map(i => i.y + i.height)) }
  async function operate(operation: ItemOperation) { await onOperation(operation); if (operation.operation === 'delete') setSelectedId(null); else if (operation.operation === 'copy' && operation.clientId) setSelectedId(operation.clientId) }
  return <>
    <div className="section-heading"><div><h2>Equipo e inventario</h2><p className="section-description inventory-intro">Mueve, equipa o copia objetos. Cada operación se comprueba antes de guardar.</p></div><button className="button subtle compact" disabled={busy} onClick={() => onCatalog({ owner: 'character', page })}><Icon name="search" size={16} />Añadir objeto</button></div>
    <div className="inventory-workspace">
      <section className="equipment-panel">
        <div className="equipment-set-tabs" role="tablist" onKeyDown={tabListKeys} aria-label="Conjuntos de armas">{([1, 2] as const).map(set => <button role="tab" aria-selected={weaponSet === set} className={weaponSet === set ? 'active' : ''} key={set} onClick={() => setWeaponSet(set)}>Equipo {set === 1 ? 'I' : 'II'}{(session.activeWeaponSet || 1) === set && <span>Activo</span>}</button>)}</div>
        <EquipmentBoard items={session.items} weaponSet={weaponSet} owner="character" busy={busy} onSelect={setSelectedId} onChoose={slot => onCatalog({ owner: 'character', slot, weaponSet })} onDrop={(itemId, slot) => void operate({ operation: 'equip', itemId, destination: { owner: 'character', page: 'equipped', slot, weaponSet } }).catch(() => undefined)} />
        <button className="button subtle compact full" disabled={busy || weaponSet === (session.activeWeaponSet || 1)} onClick={() => void onWeaponSet(weaponSet).catch(() => undefined)}>Usar el equipo {weaponSet === 1 ? 'I' : 'II'}</button>
        <p className="small muted equipment-explanation">Armadura y accesorios se comparten. El cambio de conjunto afecta a las manos y a sus habilidades asignadas.</p>
      </section>
      <section className="storage-panel">
        <div className="storage-tabs" role="tablist" onKeyDown={tabListKeys} aria-label="Almacenamiento">{(['inventory', 'stash', 'cube', 'belt'] as const).map(p => <button role="tab" key={p} aria-selected={page === p} className={page === p ? 'active' : ''} onClick={() => { setPage(p); setFilter('') }}>{p === 'stash' ? 'Baúl' : p === 'cube' ? 'Cubo' : pageNames[p]}</button>)}</div>
        <div className="storage-toolbar"><label className="search-field"><Icon name="search" size={15} /><input aria-label={`Buscar en ${pageNames[page]}`} value={filter} onChange={e => setFilter(e.target.value)} placeholder="Buscar en este espacio" /></label><div className="view-toggle"><button className={view === 'grid' ? 'active' : ''} onClick={() => setView('grid')} aria-label="Ver cuadrícula">▦</button><button className={view === 'list' ? 'active' : ''} onClick={() => setView('list')} aria-label="Ver lista">≡</button></div></div>
        {view === 'grid' ? <ItemGrid items={visibleItems} grid={actualGrid} belt={page === 'belt'} busy={busy} onSelect={setSelectedId} onDrop={(itemId, x, y) => void operate({ operation: 'move', itemId, destination: { owner: 'character', page, x: page === 'belt' ? y * 4 + x : x, y: page === 'belt' ? 0 : y } }).catch(() => undefined)} /> : <div className="storage-list">{visibleItems.map(item => <button key={item.id} data-item-id={item.id} disabled={busy} className={`storage-list-item quality-${itemQuality(item)}`} onClick={() => setSelectedId(item.id)}><span><strong>{itemName(item)}</strong><small>{qualities[itemQuality(item)] || item.quality} · {item.width}×{item.height}</small></span><span>›</span></button>)}</div>}
        <div className="storage-summary"><span>{pageItems.length} objetos · {actualGrid.width} × {actualGrid.height} casillas</span><span>Arrastra para mover · Pulsa para ver acciones</span></div>
      </section>
    </div>
    {selected && <ItemActions item={selected} session={session} busy={busy} onClose={() => setSelectedId(null)} onOperation={operate} onWorkshop={onWorkshop ? () => { onWorkshop(selected); setSelectedId(null) } : undefined} onExportBlueprint={onExportBlueprint ? () => onExportBlueprint(selected) : undefined} />}
  </>
}

export function EquipmentBoard({ items, weaponSet = 1, owner, busy, allowedSlots, onSelect, onChoose, onDrop }: { items: Item[]; weaponSet?: 1 | 2; owner: 'character' | 'mercenary'; busy: boolean; allowedSlots?: string[]; onSelect: (id: string) => void; onChoose: (slot: string) => void; onDrop: (id: string, slot: string) => void }) {
  const worn = items.filter(item => isWorn(item) && itemOwner(item) === owner && (!['weapon', 'shield'].includes(item.slot || '') || owner === 'mercenary' || (item.weaponSet || 1) === weaponSet))
  const twoHanded = worn.find(item => item.slot === 'weapon' && item.twoHanded)
  return <div className="paperdoll">{slots.map(slot => {
    const item = worn.find(i => i.slot === slot.id)
    const ghost = slot.id === 'shield' && twoHanded && !item
    const allowed = !allowedSlots || allowedSlots.includes(slot.id)
    return <div className={`equipment-slot slot-${slot.position} ${ghost ? 'two-hand-ghost' : ''} ${!allowed ? 'unavailable-slot' : ''}`} key={slot.id} onDragOver={event => { if (!busy && !ghost && allowed) event.preventDefault() }} onDrop={event => { event.preventDefault(); const id = event.dataTransfer.getData('application/x-akara-item'); if (id && !busy && !ghost && allowed) onDrop(id, slot.id) }}>
      <span className="slot-label">{slot.label}</span>{item ? <><button data-item-id={item.id} draggable={!busy} onDragStart={e => e.dataTransfer.setData('application/x-akara-item', item.id)} className={`item-name quality-${itemQuality(item)}`} disabled={busy} onClick={() => onSelect(item.id)}>{itemName(item)}</button><small>{item.twoHanded ? 'DOS MANOS' : qualities[itemQuality(item)] || item.quality}</small></> : ghost ? <><span className={`item-name quality-${itemQuality(twoHanded)}`}>{itemName(twoHanded)}</span><small>OCUPADA POR EL ARMA</small></> : allowed ? <button className="empty-slot" disabled={busy} onClick={() => onChoose(slot.id)}><span>+</span>Elegir objeto</button> : <span className="small muted">No disponible</span>}
    </div>
  })}</div>
}

export function ItemGrid({ items, grid, belt = false, busy, onSelect, onDrop }: { items: Item[]; grid: { width: number; height: number }; belt?: boolean; busy: boolean; onSelect: (id: string) => void; onDrop: (id: string, x: number, y: number) => void }) {
  return <div className={`inventory-grid storage-grid ${belt ? 'belt-grid' : ''}`} style={{ gridTemplateColumns: `repeat(${grid.width}, minmax(0, 1fr))`, '--grid-columns': grid.width, '--grid-row-count': grid.height } as CSSProperties} aria-label="Cuadrícula de objetos">
    {Array.from({ length: grid.width * grid.height }, (_, index) => <span key={`cell-${index}`} className="inventory-cell" style={{ gridColumn: index % grid.width + 1, gridRow: Math.floor(index / grid.width) + 1 }} onDragOver={e => { if (!busy) e.preventDefault() }} onDrop={e => { e.preventDefault(); const id = e.dataTransfer.getData('application/x-akara-item'); if (id && !busy) onDrop(id, index % grid.width, Math.floor(index / grid.width)) }} />)}
    {items.map(item => <button key={item.id} data-item-id={item.id} draggable={!busy} disabled={busy} aria-label={`${itemName(item)}, ${placementName(item)}`} title={`${itemName(item)} · ${qualities[itemQuality(item)] || item.quality}`} className={`inventory-item quality-${itemQuality(item)}`} style={{ gridColumn: `${(belt ? item.x % 4 : item.x) + 1} / span ${belt ? 1 : item.width}`, gridRow: `${(belt ? Math.floor(item.x / 4) : item.y) + 1} / span ${belt ? 1 : item.height}` }} onDragStart={e => e.dataTransfer.setData('application/x-akara-item', item.id)} onClick={() => onSelect(item.id)}><span>{itemAbbreviation(item)}</span></button>)}
  </div>
}

export function ItemActions({ item, session, busy, onClose, onOperation, onWorkshop, onExportBlueprint }: { item: Item; session: CharacterSession; busy: boolean; onClose: () => void; onOperation: (operation: ItemOperation) => Promise<void>; onWorkshop?: () => void; onExportBlueprint?: () => Promise<void> }) {
  const [destination, setDestination] = useState<ItemDestination>({ owner: 'character', page: 'stash' })
  const [manual, setManual] = useState(false)
  const [action, setAction] = useState<'move' | 'copy'>('move')
  const [failure, setFailure] = useState<string | null>(null)
  useEffect(() => { setDestination({ owner: 'character', page: item.page === 'stash' ? 'inventory' : 'stash' }); setManual(false); setFailure(null) }, [item.id, item.page])
  const allStats = [...(item.stats || []), ...(item.runewordStats || [])].filter(stat => !stat.hidden)
  const permittedSlots = slots.filter(slot => (!item.equipSlots?.length || item.equipSlots.includes(slot.id)) && (destination.owner !== 'mercenary' || !session.mercenary?.allowedSlots || session.mercenary.allowedSlots.includes(slot.id)))
  const sockets = item.sockets?.filter((socket): socket is Item => !!socket) || []
  async function apply(remove = false) {
    setFailure(null)
    try { await onOperation(remove ? { operation: 'delete', itemId: item.id } : { operation: action === 'move' && destination.page === 'equipped' ? 'equip' : action, itemId: item.id, destination: { ...destination, ...(manual ? { x: destination.page === 'belt' ? (destination.y || 0) * 4 + (destination.x || 0) : destination.x || 0, y: destination.page === 'belt' ? 0 : destination.y || 0 } : { x: undefined, y: undefined }) }, ...(action === 'copy' ? { clientId: `n${crypto.randomUUID().replaceAll('-', '')}` } : {}) }); onClose() }
    catch (error) { setFailure(error instanceof Error ? error.message : 'No se pudo preparar la operación.') }
  }
  async function exportBlueprint() { setFailure(null); try { await onExportBlueprint?.() } catch (reason) { setFailure(reason instanceof Error ? reason.message : 'No se pudo exportar el plano.') } }
  return <Modal title={itemName(item)} subtitle={`${qualities[itemQuality(item)] || item.quality} · ${placementName(item)}`} onClose={onClose} wide>
    <div className="item-action-layout"><section className="item-action-properties"><dl className="item-metadata"><div><dt>Base</dt><dd>{item.baseName || item.name}</dd></div><div><dt>Nivel de objeto</dt><dd>{item.level ?? '—'}</dd></div><div><dt>Tamaño</dt><dd>{item.width} × {item.height}</dd></div>{item.quantity != null && <div><dt>Cantidad</dt><dd>{item.quantity}</dd></div>}{item.maxDurability != null && <div><dt>Durabilidad</dt><dd>{item.durability} / {item.maxDurability}</dd></div>}<div><dt>Huecos ocupados</dt><dd>{sockets.length}</dd></div></dl>
      <div className="item-properties"><h3>Propiedades</h3>{allStats.map((stat, index) => <div key={`${stat.id}-${stat.layer}-${index}`}><span>{stat.label || stat.name || 'Propiedad'}</span><strong>{String(stat.displayValue ?? stat.value ?? '—')}</strong></div>)}{!allStats.length && <p className="small muted">Sin propiedades adicionales guardadas.</p>}</div>{sockets.length > 0 && <div className="socket-contents"><h4>Engarces</h4>{sockets.map(socket => <span key={socket.id}>{itemName(socket)}</span>)}</div>}{onWorkshop && <button className="button subtle full" disabled={busy} onClick={onWorkshop}>Editar en el taller</button>}{onExportBlueprint && <button className="button subtle full" disabled={busy} onClick={() => void exportBlueprint()}>Exportar plano</button>}</section>
      <section className="item-action-form"><h3>Preparar una operación</h3><div className="operation-toggle"><button className={action === 'move' ? 'active' : ''} onClick={() => setAction('move')}>Mover</button><button className={action === 'copy' ? 'active' : ''} onClick={() => setAction('copy')}>Copiar</button></div><label>Destino<select value={destination.owner} onChange={e => setDestination({ owner: e.target.value as ItemDestination['owner'], page: e.target.value === 'mercenary' ? 'equipped' : 'inventory', slot: e.target.value === 'mercenary' ? permittedSlots[0]?.id || 'weapon' : undefined })}><option value="character">Personaje</option><option value="mercenary" disabled={!session.mercenary?.exists}>Mercenario</option></select></label>{destination.owner === 'character' && <label>Espacio<select value={destination.page} onChange={e => setDestination({ ...destination, page: e.target.value as ItemDestination['page'], slot: e.target.value === 'equipped' ? permittedSlots[0]?.id : undefined })}>{Object.entries(pageNames).map(([value, name]) => <option key={value} value={value}>{name}</option>)}</select></label>}
        {destination.page === 'equipped' && <><label>Ranura<select value={destination.slot || permittedSlots[0]?.id || ''} onChange={e => setDestination({ ...destination, slot: e.target.value })}>{permittedSlots.map(slot => <option value={slot.id} key={slot.id}>{slot.label}</option>)}</select></label>{destination.owner === 'character' && ['weapon', 'shield'].includes(destination.slot || permittedSlots[0]?.id || '') && <label>Conjunto<select value={destination.weaponSet || 1} onChange={e => setDestination({ ...destination, weaponSet: Number(e.target.value) as 1 | 2 })}><option value={1}>Equipo I</option><option value={2}>Equipo II</option></select></label>}</>}
        {destination.page !== 'equipped' && <><label className="inline-check"><input type="checkbox" checked={manual} onChange={e => setManual(e.target.checked)} />Elegir casilla exacta</label>{manual && <div className="coordinate-fields"><label>Columna<input type="number" min={1} value={(destination.x || 0) + 1} onChange={e => setDestination({ ...destination, x: Math.max(0, (Number(e.target.value) || 1) - 1) })} /></label><label>Fila<input type="number" min={1} value={(destination.y || 0) + 1} onChange={e => setDestination({ ...destination, y: Math.max(0, (Number(e.target.value) || 1) - 1) })} /></label></div>}<p className="small muted">{manual ? 'La revisión comprueba el tamaño y las casillas ocupadas.' : 'AKARA buscará una posición libre que respete el tamaño del objeto.'}</p></>}
        {failure && <div role="alert" className="notice error"><p>{failure}</p></div>}<button className="button primary full" disabled={busy || destination.page === 'equipped' && !permittedSlots.length} onClick={() => void apply()}>{busy ? 'Comprobando…' : action === 'copy' ? 'Preparar copia' : 'Preparar traslado'}</button><button className="delete-item-button" disabled={busy} onClick={() => void apply(true)}>Marcar para eliminar</button><p className="small muted">Las operaciones quedan en la vista previa. Puedes deshacerlas antes de guardar.</p>
      </section></div>
  </Modal>
}
