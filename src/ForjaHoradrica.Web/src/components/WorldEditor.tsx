import { useEffect, useState } from 'react'
import { api } from '../api'
import type { Difficulty, Quest, QuestState, WorldChange, WorldMetadata } from '../api'
import { Icon, tabListKeys } from './ui'

const difficultyNames: Record<Difficulty, string> = { normal: 'Normal', nightmare: 'Pesadilla', hell: 'Infierno' }
const stateNames: Record<QuestState, string> = { notStarted: 'Pendiente', started: 'En curso', completed: 'Objetivo completado', rewarded: 'Recompensa cobrada' }
const acts = ['', 'I', 'II', 'III', 'IV', 'V']
const displayQuest = (quest: Quest) => quest.id.endsWith('.introduced') ? 'Acceso al acto' : quest.id.endsWith('.completion') ? 'Acto completado' : quest.name

export default function WorldEditor({ sessionId, planId, busy, onChange }: { sessionId: string; planId?: string; busy: boolean; onChange: (change: WorldChange) => Promise<void> }) {
  const [world, setWorld] = useState<WorldMetadata | null>(null)
  const [difficulty, setDifficulty] = useState<Difficulty>('normal')
  const [section, setSection] = useState<'quests' | 'waypoints'>('quests')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [activeDifficulty, setActiveDifficulty] = useState<Difficulty>('normal')
  const [maximum, setMaximum] = useState<Difficulty>('normal')
  const [act, setAct] = useState(1)
  const current = world?.difficulties.find(entry => entry.id === difficulty)
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError(null)
    void api<WorldMetadata>(`/world?sessionId=${encodeURIComponent(sessionId)}${planId ? `&planId=${encodeURIComponent(planId)}` : ''}`, undefined, controller.signal).then(metadata => { setWorld(metadata); setActiveDifficulty(metadata.activeDifficulty); setMaximum(metadata.maxDifficulty); setAct(metadata.act) }).catch(reason => { if (reason.name !== 'AbortError') setError(reason.message) }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [sessionId, planId])
  async function change(patch: WorldChange) { setError(null); try { await onChange(patch) } catch (reason) { setError(reason instanceof Error ? reason.message : 'No se pudo preparar el progreso.') } }
  return <>
    <div className="section-heading"><div><h2>Mundo y progreso</h2><p className="section-description inventory-intro">Misiones, transportadores y lugar de inicio por dificultad.</p></div></div>
    {loading && <p className="loading-line"><span className="spinner" />Leyendo el progreso de la partida…</p>}{error && <div role="alert" className="notice error"><p>{error}</p></div>}
    {world && <><section className="world-start"><div><h3>Lugar de inicio</h3><p>Elige dónde aparecerá el personaje al abrir la partida.</p></div><label>Dificultad<select aria-label="Dificultad de inicio" value={activeDifficulty} disabled={busy || loading} onChange={event => setActiveDifficulty(event.target.value as Difficulty)}>{Object.entries(difficultyNames).map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></label><label>Acto<select aria-label="Acto de inicio" value={act} disabled={busy || loading} onChange={event => setAct(Number(event.target.value))}>{[1, 2, 3, 4, 5].map(value => <option key={value} value={value}>Acto {acts[value]}</option>)}</select></label><label>Acceso hasta<select aria-label="Dificultad máxima" value={maximum} disabled={busy || loading} onChange={event => setMaximum(event.target.value as Difficulty)}>{Object.entries(difficultyNames).map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></label><button className="button subtle compact" disabled={busy || loading} onClick={() => void change({ activeDifficulty, maxDifficulty: maximum, act })}>Preparar inicio</button></section>
      <div className="world-toolbar"><div className="difficulty-tabs" role="tablist" onKeyDown={tabListKeys} aria-label="Dificultad del progreso">{Object.entries(difficultyNames).map(([id, name]) => <button role="tab" aria-selected={difficulty === id} className={difficulty === id ? 'active' : ''} key={id} onClick={() => setDifficulty(id as Difficulty)}>{name}</button>)}</div><div className="operation-toggle"><button aria-pressed={section === 'quests'} className={section === 'quests' ? 'active' : ''} onClick={() => setSection('quests')}>Misiones</button><button aria-pressed={section === 'waypoints'} className={section === 'waypoints' ? 'active' : ''} onClick={() => setSection('waypoints')}>Transportadores</button></div></div>
      {section === 'quests' ? <div className="quest-acts">{[1, 2, 3, 4, 5].map(act => <section key={act} className="quest-act"><div className="panel-title"><h3>Acto {acts[act]}</h3><span>{current?.quests.filter(quest => quest.act === act && quest.state === 'rewarded').length || 0} recompensas cobradas</span></div>{current?.quests.filter(quest => quest.act === act).map(quest => <div className="quest-row" key={quest.id}><span><strong>{displayQuest(quest)}</strong>{quest.anyaConsumed && <small><Icon name="check" size={12} />Pergamino de resistencia consumido</small>}</span><select aria-label={`Estado de ${displayQuest(quest)}`} value={quest.state} disabled={busy || loading} onChange={event => void change({ quests: [{ difficulty, questId: quest.id, state: event.target.value as QuestState }] })}>{Object.entries(stateNames).map(([state, name]) => <option key={state} value={state}>{name}</option>)}</select></div>)}</section>)}</div> : <><div className="waypoint-actions"><span>{current?.waypoints.filter(waypoint => waypoint.active).length || 0} / {current?.waypoints.length || 0} activados en {difficultyNames[difficulty]}</span><button className="text-button" disabled={busy || loading} onClick={() => void change({ waypoints: current?.waypoints.map(waypoint => ({ difficulty, id: waypoint.id, active: true })) || [] })}>Activar todos</button><button className="text-button" disabled={busy || loading} onClick={() => void change({ waypoints: current?.waypoints.map(waypoint => ({ difficulty, id: waypoint.id, active: false })) || [] })}>Desactivar todos</button></div><div className="waypoint-acts">{[1, 2, 3, 4, 5].map(act => <section className="waypoint-act" key={act}><h3>Acto {acts[act]}</h3>{current?.waypoints.filter(waypoint => waypoint.act === act).map(waypoint => <label className="inline-check" key={waypoint.id}><input type="checkbox" aria-label={`Transportador ${waypoint.name}`} checked={waypoint.active} disabled={busy || loading} onChange={event => void change({ waypoints: [{ difficulty, id: waypoint.id, active: event.target.checked }] })} />{waypoint.name}</label>)}</section>)}</div></>}
      <p className="world-reward-note"><Icon name="shield" size={17} />Las recompensas de misiones se concilian con los puntos y recursos del personaje. La revisión detecta asignaciones que exceden lo disponible.</p>
    </>}
  </>
}
