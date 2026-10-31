import { useState } from 'react'
import { api } from '../api'
import type { Bootstrap, CharacterSession, Preview } from '../api'
import { Icon, Modal, Stepper, tabListKeys } from './ui'

const classes = [
  { native: 'Amazon', name: 'Amazona', initial: 'A', description: 'Lanzas, jabalinas y arcos.' },
  { native: 'Sorceress', name: 'Hechicera', initial: 'H', description: 'Fuego, relámpago y frío.' },
  { native: 'Necromancer', name: 'Nigromante', initial: 'N', description: 'Invocación, hueso y maldiciones.' },
  { native: 'Paladin', name: 'Paladín', initial: 'P', description: 'Combate y auras.' },
  { native: 'Barbarian', name: 'Bárbaro', initial: 'B', description: 'Armas y gritos de guerra.' },
  { native: 'Druid', name: 'Druida', initial: 'D', description: 'Elementos, formas e invocación.' },
  { native: 'Assassin', name: 'Asesina', initial: 'A', description: 'Trampas y artes marciales.' },
  { native: 'Warlock', name: 'Brujo', initial: 'B', description: 'Demonios y artes oscuras.' },
]

export default function CharacterWizard({ bootstrap, current, preview, initialMode = 'new', onReady, onClose }: { bootstrap: Bootstrap; current: CharacterSession | null; preview: Preview | null; initialMode?: 'new' | 'clone'; onReady: (session: CharacterSession) => void; onClose: () => void }) {
  const [mode, setMode] = useState(initialMode)
  const [profileId, setProfileId] = useState(current?.profileId || bootstrap.defaultProfileId || bootstrap.profiles[0]?.id || '')
  const [className, setClassName] = useState('Sorceress')
  const [name, setName] = useState('')
  const [level, setLevel] = useState(1)
  const [includePreview, setIncludePreview] = useState(!!preview?.canSave)
  const [preset, setPreset] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const validName = /^[A-Za-z_-]{2,15}$/.test(name)
  const selectedProfile = bootstrap.profiles.find(profile => profile.id === profileId)
  const availableClasses = classes.filter(characterClass => !selectedProfile?.classes || selectedProfile.classes.some(metadata => metadata.name === characterClass.native))
  const availablePresets = bootstrap.presets.filter(entry => entry.className === className).sort((a, b) => Number(b.id === 'squad-sorceress') - Number(a.id === 'squad-sorceress'))
  const maxLevel = selectedProfile?.classes?.find(metadata => metadata.name === className)?.maxLevel || 99
  async function create() {
    if (!validName) return
    setBusy(true); setError(null)
    try {
      const result = mode === 'clone' && current ? await api<CharacterSession>('/clone', { sessionId: current.sessionId, name, ...(includePreview && preview?.canSave ? { planId: preview.planId } : {}) }) : await api<CharacterSession>('/create', { profileId, className, name, level, version: 105, ...(preset ? { preset } : {}) })
      onReady(result)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'No se pudo preparar el personaje.') }
    finally { setBusy(false) }
  }
  return <Modal title={mode === 'clone' ? 'Clonar personaje' : 'Crear un personaje'} subtitle="Prepara una partida independiente. El archivo se creará cuando lo guardes o exportes." wide onClose={onClose}>
    <div className="character-wizard"><div className="wizard-modes" role="tablist" onKeyDown={tabListKeys} aria-label="Origen del personaje"><button role="tab" aria-selected={mode === 'new'} className={mode === 'new' ? 'active' : ''} disabled={busy} onClick={() => setMode('new')}>Personaje nuevo</button><button role="tab" aria-selected={mode === 'clone'} className={mode === 'clone' ? 'active' : ''} disabled={busy || !current} onClick={() => setMode('clone')}>Clonar el personaje abierto</button></div>
      <div className="wizard-form"><section className="wizard-main">{mode === 'new' ? <><label>Perfil de juego<select value={profileId} disabled={busy} onChange={event => setProfileId(event.target.value)}>{bootstrap.profiles.map(profile => <option key={profile.id} value={profile.id}>{profile.name}</option>)}</select></label><fieldset className="class-picker"><legend>Clase</legend>{availableClasses.map(characterClass => <button key={characterClass.native} type="button" aria-pressed={className === characterClass.native} className={className === characterClass.native ? 'selected' : ''} disabled={busy} onClick={() => { setClassName(characterClass.native); setPreset('') }}><span className="class-initial" aria-hidden="true">{characterClass.initial}</span><span><strong>{characterClass.name}</strong><small>{characterClass.description}</small></span></button>)}</fieldset></> : <div className="clone-summary"><p className="eyebrow">PERSONAJE ORIGINAL</p><h3>{current?.character.name}</h3><p>{classes.find(characterClass => characterClass.native === current?.character.className)?.name || current?.character.className} · Nivel {current?.character.level}</p><p className="small muted">La copia conserva su propio equipo, habilidades y progreso. El original se mantiene intacto.</p>{preview?.canSave && <label className="inline-check"><input type="checkbox" checked={includePreview} onChange={event => setIncludePreview(event.target.checked)} disabled={busy} />Usar la vista previa validada</label>}</div>}</section>
        <section className="wizard-details"><label htmlFor="new-character-name">Nombre del nuevo personaje<input id="new-character-name" autoComplete="off" value={name} maxLength={15} disabled={busy} onChange={event => setName(event.target.value)} placeholder="Nombre del personaje" /><small>Entre 2 y 15 letras. Se permiten guion y guion bajo.</small></label>{mode === 'new' && <><label>Configuración inicial<select value={preset} disabled={busy} onChange={event => setPreset(event.target.value)}><option value="">Personaje sin plantilla</option>{availablePresets.map(entry => <option key={entry.id} value={entry.id}>{entry.name}</option>)}</select></label><label>Nivel inicial<Stepper label="Nivel inicial" value={preset ? 85 : level} min={1} max={maxLevel} disabled={busy || !!preset} onChange={setLevel} /></label>{preset && <p className="small muted">La plantilla prepara equipo, habilidades y progreso de nivel85. Podrás revisar cada parte antes de guardar.</p>}</>}<div className="wizard-assurance"><Icon name="shield" /><p>Se usarán los atributos, recursos y límites nativos del perfil. Podrás revisar el resultado antes de crear el archivo.</p></div>{error && <div role="alert" className="notice error"><p>{error}</p></div>}<button className="button primary full" disabled={busy || !validName || mode === 'new' && !profileId} onClick={() => void create()}>{busy ? 'Preparando personaje…' : mode === 'clone' ? 'Preparar copia' : 'Preparar personaje'}<Icon name="arrow" size={17} /></button></section>
      </div>
    </div>
  </Modal>
}
