import type { Item } from '../api'

export const slots = [
  { id: 'helm', label: 'Casco', position: 'head' }, { id: 'amulet', label: 'Amuleto', position: 'neck' },
  { id: 'weapon', label: 'Arma', position: 'weapon' }, { id: 'armor', label: 'Armadura', position: 'armor' },
  { id: 'shield', label: 'Mano izquierda', position: 'shield' }, { id: 'gloves', label: 'Guantes', position: 'gloves' },
  { id: 'belt', label: 'Cinturón', position: 'belt' }, { id: 'boots', label: 'Botas', position: 'boots' },
  { id: 'ring1', label: 'Anillo derecho', position: 'ring-one' }, { id: 'ring2', label: 'Anillo izquierdo', position: 'ring-two' },
]
export const qualities: Record<string, string> = { unique: 'Único', set: 'Conjunto', rare: 'Raro', magic: 'Mágico', normal: 'Normal', superior: 'Superior', runeword: 'Palabra rúnica', crafted: 'Artesanal', craft: 'Artesanal', lowquality: 'Baja calidad', inferior: 'Baja calidad' }
export const pageNames: Record<string, string> = { inventory: 'Inventario', stash: 'Baúl personal', cube: 'Cubo horádrico', belt: 'Cinturón', equipped: 'Equipo' }
export const itemQuality = (item: Item) => item.runewordName ? 'runeword' : item.quality.toLowerCase() === 'craft' ? 'crafted' : item.quality.toLowerCase()
export const itemName = (item: Item) => item.name || item.baseName || 'Objeto'
export const isWorn = (item: Item) => item.page === 'equipped' || (!!item.slot && !['none', 'null', ''].includes(item.slot.toLowerCase()))
export const itemOwner = (item: Item) => item.owner || (item.id.startsWith('m') ? 'mercenary' : 'character')
export const slotName = (slot?: string) => slots.find(s => s.id === slot)?.label || 'Sin ranura'
export const placementName = (item: Item) => isWorn(item) ? `${itemOwner(item) === 'mercenary' ? 'Mercenario' : `Equipo ${item.weaponSet === 2 ? 'II' : 'I'}`} · ${slotName(item.slot)}` : pageNames[item.page || 'inventory'] || 'Inventario'
export const itemAbbreviation = (item: Item) => {
  if (item.name === 'Annihilus') return 'Anni'
  if (item.name === 'Hellfire Torch') return 'Torch'
  if (item.name.includes('Gheed')) return 'Gheed'
  if (item.code === 'tbk') return 'Portal'
  if (item.code === 'ibk') return 'Ident.'
  if (item.code === 'box') return 'Cubo'
  if (item.code === 'cm1') return 'TC'
  if (item.code === 'cm2') return 'TM'
  if (item.code === 'cm3') return 'TG'
  return item.width > 1 ? itemName(item) : itemName(item).split(' ').find(s => s.length > 3)?.slice(0, 5) || itemName(item).slice(0, 5)
}
