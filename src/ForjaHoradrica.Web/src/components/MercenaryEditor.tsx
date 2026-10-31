import { useEffect, useState } from 'react'
import { api } from '../api'
import type { CharacterSession, Changes, Hireling, Item, ItemOperation } from '../api'
import { EquipmentBoard, ItemActions } from './InventoryEditor'
import type { CatalogTarget } from './InventoryEditor'
import { Icon, Stepper } from './ui'

const acts = ['', 'I', 'II', 'III', 'IV', 'V']
const resourceLabels: Record<string, string> = { life: 'Vida base', maxLife: 'Vida máxima base', mana: 'Maná base', strength: 'Fuerza', dexterity: 'Destreza', vitality: 'Vitalidad', energy: 'Energía', fire: 'Fuego', cold: 'Frío', lightning: 'Rayos', poison: 'Veneno', defense: 'Defensa base' }

export default function MercenaryEditor({ session, busy, onChange, onOperation, onCatalog, onWorkshop, onExportBlueprint }: { session: CharacterSession; busy: boolean; onChange: (change: NonNullable<Changes['mercenary']>) => Promise<void>; onOperation: (operation: ItemOperation) => Promise<void>; onCatalog: (target: CatalogTarget) => void; onWorkshop: (item: Item) => void; onExportBlueprint: (item: Item) => Promise<void> }) {
  const merc = session.mercenary
  const [hirelings, setHirelings] = useState<Hireling[]>([])
  const [typeId, setTypeId] = useState(merc?.typeId || 0)
  const [level, setLevel] = useState(merc?.level || Math.max(1, session.character.level))
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const selected = merc?.items.find(item => item.id === selectedId)
  useEffect(() => { setTypeId(merc?.typeId || 0); setLevel(merc?.level || Math.max(1, session.character.level)) }, [merc?.typeId, merc?.level, session.character.level])
  useEffect(() => {
    const controller = new AbortController(); setLoading(true)
    void api<{ entries: Hireling[] }>(`/catalog?sessionId=${encodeURIComponent(session.sessionId)}&type=hireling&limit=250`, undefined, controller.signal).then(result => { setHirelings(result.entries); if (!merc?.exists && result.entries.length) setTypeId(result.entries[0].nativeId) }).catch(reason => { if (reason.name !== 'AbortError') setError(reason.message) }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [session.sessionId, merc?.exists])
  async function apply(change: NonNullable<Changes['mercenary']>) { setError(null); try { await onChange(change) } catch (reason) { setError(reason instanceof Error ? reason.message : 'No se pudo preparar el mercenario.') } }
  const stats = [...Object.entries(merc?.attributes || {}), ...Object.entries(merc?.resources || {}), ...Object.entries(merc?.resists || {}).map(([key, value]) => [`resist-${key}`, value] as const)]
  return <>
    <div className="section-heading"><div><h2>Mercenario</h2><p className="section-description inventory-intro">Compañero, nivel y equipo, con los límites del perfil seleccionado.</p></div>{merc?.exists && <span className={`budget-pill ${merc.dead ? 'invalid' : ''}`}>{merc.dead ? 'Muerto' : 'Vivo'} · Nivel {merc.level}</span>}</div>
    {error && <div role="alert" className="notice error"><p>{error}</p></div>}
    <div className="mercenary-layout">
      <section className="mercenary-settings"><div className="panel-title"><h3>{merc?.exists ? merc.typeName || 'Compañero contratado' : 'Contratar un compañero'}</h3>{merc?.act && <span>Acto {acts[merc.act] || merc.act}</span>}</div>
        {merc?.aura && <div className="mercenary-aura"><Icon name="shield" size={17} /><span>{merc.aura}</span></div>}
        <label>Tipo de mercenario<select value={typeId} disabled={loading || busy} onChange={e => setTypeId(Number(e.target.value))}>{hirelings.map(hireling => <option key={hireling.nativeId} value={hireling.nativeId}>{hireling.name}{hireling.act ? ` · Acto ${acts[hireling.act] || hireling.act}` : ''}{hireling.aura ? ` · ${hireling.aura}` : ''}</option>)}{loading && <option value={typeId}>Leyendo tipos nativos…</option>}</select></label>
        <label>Nivel<Stepper label="Nivel del mercenario" value={level} min={1} max={session.character.level} disabled={busy} onChange={setLevel} /></label>
        <button className="button primary full" disabled={busy || loading || !hirelings.length} onClick={() => void apply({ hirelingId: typeId, level })}>{merc?.exists ? 'Preparar mercenario' : 'Preparar contratación'}</button>{merc?.exists && merc.dead && <button className="button subtle full" disabled={busy} onClick={() => void apply({ resurrect: true })}>Resucitar en la vista previa</button>}
        {merc?.exists && <dl className="mercenary-native-stats"><div><dt>Experiencia</dt><dd>{new Intl.NumberFormat('es-ES').format(merc.experience)}</dd></div>{stats.map(([key, value]) => <div key={key}><dt>{key.startsWith('resist-') ? `Resistencia: ${resourceLabels[key.slice(7)] || key.slice(7)}` : resourceLabels[key] || key}</dt><dd>{new Intl.NumberFormat('es-ES').format(value)}</dd></div>)}</dl>}
        <p className="small muted">{stats.length ? 'Valores nativos antes de las bonificaciones de equipo.' : 'Los recursos y las bonificaciones finales del mercenario se calculan dentro del juego.'}</p>
      </section>
      <section className="equipment-panel mercenary-equipment"><div className="panel-title"><h3>Equipo del mercenario</h3><span>{merc?.items.length || 0} piezas</span></div><EquipmentBoard owner="mercenary" items={merc?.items || []} busy={busy || !merc?.exists} allowedSlots={merc?.allowedSlots} onSelect={setSelectedId} onChoose={slot => onCatalog({ owner: 'mercenary', slot })} onDrop={(itemId, slot) => void onOperation({ operation: 'equip', itemId, destination: { owner: 'mercenary', page: 'equipped', slot } }).catch(() => undefined)} /><p className="small muted equipment-explanation">Un arma a dos manos ocupa ambas manos. Su reflejo en la otra ranura representa la misma pieza.</p></section>
    </div>
    {selected && <ItemActions item={selected} session={session} busy={busy} onClose={() => setSelectedId(null)} onOperation={onOperation} onWorkshop={() => { onWorkshop(selected); setSelectedId(null) }} onExportBlueprint={() => onExportBlueprint(selected)} />}
  </>
}
