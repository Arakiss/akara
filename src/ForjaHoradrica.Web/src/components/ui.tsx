import { useEffect, useRef } from 'react'
import type { KeyboardEvent as ReactKeyboardEvent } from 'react'

export function tabListKeys(event: ReactKeyboardEvent<HTMLElement>) {
  if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return
  const tabs = [...event.currentTarget.querySelectorAll<HTMLButtonElement>('button[role="tab"]:not(:disabled)')]
  const selected = (event.target as HTMLElement).closest('button[role="tab"]')
  const index = tabs.findIndex(tab => tab === selected)
  if (index < 0 || !tabs.length) return
  event.preventDefault()
  const next = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 : (index + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length
  tabs[next].focus(); tabs[next].click()
}

export function Icon({ name, size = 18 }: { name: 'refresh' | 'search' | 'close' | 'arrow' | 'check' | 'folder' | 'shield' | 'save'; size?: number }) {
  const paths: Record<string, React.ReactNode> = {
    refresh: <><path d="M20 7v5h-5M4 17v-5h5" /><path d="M6.1 7a7 7 0 0 1 11.5-2L20 8M4 16l2.4 3A7 7 0 0 0 18 17" /></>,
    search: <><circle cx="10.5" cy="10.5" r="6.5" /><path d="m16 16 5 5" /></>,
    close: <path d="m6 6 12 12M6 18 18 6" />,
    arrow: <path d="M4 12h15m-6-6 6 6-6 6" />,
    check: <path d="m5 12 4 4L19 6" />,
    folder: <path d="M3 6h6l2 2h10v12H3V6Z" />,
    shield: <><path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6l8-3Z" /><path d="m8 12 3 3 5-6" /></>,
    save: <><path d="M4 3h13l4 4v14H3V3Z" /><path d="M7 3v6h9V3M7 21v-8h10v8" /></>,
  }
  return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{paths[name]}</svg>
}
export function Stepper({ value, onChange, min = 0, max = 999, label, disabled = false }: { value: number; onChange: (n: number) => void; min?: number; max?: number; label: string; disabled?: boolean }) {
  return <div className="stepper">
    <button type="button" aria-label={`Reducir ${label}`} disabled={disabled || value <= min} onClick={() => onChange(Math.max(min, value - 1))}>−</button>
    <input type="number" aria-label={label} min={min} max={max} value={value} disabled={disabled} onChange={e => { if (e.target.value !== '') onChange(Math.max(min, Math.min(max, Math.trunc(Number(e.target.value) || 0)))) }} />
    <button type="button" aria-label={`Aumentar ${label}`} disabled={disabled || value >= max} onClick={() => onChange(Math.min(max, value + 1))}>+</button>
  </div>
}
export function Modal({ title, subtitle, children, onClose, wide = false }: { title: string; subtitle?: string; children: React.ReactNode; onClose: () => void; wide?: boolean }) {
  const panel = useRef<HTMLDivElement>(null)
  const close = useRef(onClose)
  close.current = onClose
  useEffect(() => {
    const last = document.activeElement as HTMLElement | null
    const firstInput = panel.current?.querySelector<HTMLInputElement>('input')
    if (firstInput) firstInput.focus(); else panel.current?.focus()
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') close.current()
      if (event.key === 'Tab') {
        const nodes = panel.current?.querySelectorAll<HTMLElement>('button:not(:disabled),input:not(:disabled),select:not(:disabled),[tabindex="0"]')
        if (!nodes?.length) return
        const first = nodes[0], end = nodes[nodes.length - 1]
        if (event.shiftKey && (document.activeElement === first || document.activeElement === panel.current)) { event.preventDefault(); end.focus() }
        else if (!event.shiftKey && document.activeElement === end) { event.preventDefault(); first.focus() }
      }
    }
    document.addEventListener('keydown', onKey)
    return () => { document.removeEventListener('keydown', onKey); last?.focus() }
  }, [])
  return <div className="modal-backdrop" onMouseDown={e => { if (e.target === e.currentTarget) onClose() }}>
    <div ref={panel} tabIndex={-1} role="dialog" aria-modal="true" aria-label={title} className={`modal ${wide ? 'modal-wide' : ''}`}>
      <header className="modal-header"><div><p className="eyebrow">AKARA</p><h2>{title}</h2>{subtitle && <p className="muted">{subtitle}</p>}</div><button className="icon-button" aria-label="Cerrar ventana" onClick={onClose}><Icon name="close" /></button></header>
      {children}
    </div>
  </div>
}
