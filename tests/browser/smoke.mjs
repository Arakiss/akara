import { chromium } from 'playwright'
import assert from 'node:assert/strict'
import fs from 'node:fs/promises'
import path from 'node:path'
import crypto from 'node:crypto'

const { AKARA_TEST_URL: suppliedUrl, AKARA_TEST_SAVE_PATH: suppliedSave, AKARA_TEST_DEST_DIR: suppliedDestination } = process.env
if (!suppliedUrl || !suppliedSave || !suppliedDestination) throw new Error('Set AKARA_TEST_URL, AKARA_TEST_SAVE_PATH and AKARA_TEST_DEST_DIR. Use a private copied save and an isolated server --data-dir.')
const address = new URL(suppliedUrl)
if (!['127.0.0.1', 'localhost', '[::1]'].includes(address.hostname)) throw new Error('Browser checks require a local loopback application.')
const token = process.env.AKARA_TEST_TOKEN || address.searchParams.get('token')
if (!token) throw new Error('Supply the local application token.')
address.searchParams.set('token', token)
const source = path.resolve(suppliedSave)
const destinationRoot = path.resolve(suppliedDestination)
const inSavedGames = value => value.split(/[\\/]/).some(part => part.replaceAll(' ', '').toLowerCase() === 'savedgames')
if (inSavedGames(source) || inSavedGames(destinationRoot)) throw new Error('Source and outputs must be outside Saved Games.')
const sourceBytes = await fs.readFile(source)
const digest = value => crypto.createHash('sha256').update(value).digest('hex')
const originalHash = digest(sourceBytes)
await fs.mkdir(destinationRoot, { recursive: true })
const output = await fs.mkdtemp(path.join(destinationRoot, 'browser-'))
const copyDirectory = path.join(output, 'source-copy')
await fs.mkdir(copyDirectory)
let workingSave = path.join(copyDirectory, path.basename(source))
await fs.writeFile(workingSave, sourceBytes, { flag: 'wx' })
const checks = [], skipped = [], errors = [], libraryIds = new Set()
const safeError = error => String(error instanceof Error ? error.message : error).replaceAll(token, '[local-token]')
let browser, page, context, current
let workingRestored = true

try {
  browser = await chromium.launch({ headless: true, ...(process.env.AKARA_TEST_BROWSER_CHANNEL ? { channel: process.env.AKARA_TEST_BROWSER_CHANNEL } : {}) })
  context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, permissions: ['clipboard-read', 'clipboard-write'] })
  page = await context.newPage()
  page.setDefaultTimeout(15000)
  page.on('pageerror', error => errors.push(safeError(error)))
  const tab = name => page.getByRole('tab', { name, exact: true })
  const button = name => page.getByRole('button', { name, exact: true })
  const responseFor = (route, method = 'POST') => page.waitForResponse(response => new URL(response.url()).pathname === `/api/${route}` && response.request().method() === method)
  async function action(route, perform, method = 'POST') {
    const [response] = await Promise.all([responseFor(route, method), perform()])
    const data = await response.json()
    assert.equal(response.status(), 200, `${route}: ${data.message || JSON.stringify(data)}`)
    return data
  }
  async function preview(perform) {
    const result = await action('preview', perform)
    assert.equal(result.canSave, true, JSON.stringify(result.validation.errors))
    current = result.summary
    return result
  }
  async function request(route) {
    const response = await context.request.get(new URL(`/api/${route}`, address).toString(), { headers: { 'X-Forja-Token': token } })
    const result = await response.json()
    assert.equal(response.status(), 200, result.message || route)
    return result
  }
  async function open(savePath) {
    await button('Abrir otra partida').click()
    await page.getByLabel('Archivo del personaje', { exact: true }).fill(savePath)
    if (process.env.AKARA_TEST_EXCEL) {
      await page.getByText('Indicar la carpeta del mod', { exact: true }).click()
      await page.getByLabel('Carpeta de tablas del juego', { exact: false }).fill(process.env.AKARA_TEST_EXCEL)
    }
    current = await action('open', () => page.getByRole('dialog', { name: 'Abrir un personaje', exact: true }).getByRole('button', { name: 'Abrir personaje', exact: true }).click())
    assert.equal(current.validation.roundtrip, true)
    return current
  }
  async function undo() { return preview(() => button('Deshacer').click()) }
  async function closeModal() { await page.getByRole('dialog').getByRole('button', { name: 'Cerrar ventana', exact: true }).click() }
  async function item(id) { const located = page.locator(`[data-item-id="${id}"]`); const entry = [...current.items, ...(current.mercenary?.items || [])].find(value => value.id === id); if (await located.count()) await located.first().click(); else await page.getByRole('button', { name: entry.page === 'equipped' ? entry.name : `${entry.name}, ${{ inventory: 'Inventario', stash: 'Baúl personal', cube: 'Cubo horádrico', belt: 'Cinturón' }[entry.page]}`, exact: true }).first().click() }
  async function download(name, fileName) {
    const [file] = await Promise.all([page.waitForEvent('download'), button(name).click()])
    const target = path.join(output, fileName)
    await file.saveAs(target)
    return { target, document: JSON.parse(await fs.readFile(target, 'utf8')) }
  }
  async function capture(fileName) {
    await page.locator('.editor-layout').evaluate(element => { element.scrollTop = 0 })
    await page.screenshot({ path: path.join(output, fileName), fullPage: true })
  }
  async function library() { await button('Ver plantillas y equipos').click(); await page.getByRole('dialog', { name: 'Plantillas y configuraciones', exact: true }).waitFor() }
  async function create(name, preset = '') {
    await button('Nuevo personaje').click()
    const dialog = page.getByRole('dialog', { name: 'Crear un personaje', exact: true })
    assert.equal(await dialog.locator('.class-picker button').count(), 8, 'Reimagined profile must expose eight native classes')
    await dialog.getByLabel('Nombre del nuevo personaje', { exact: false }).fill(name)
    await dialog.getByLabel('Configuración inicial', { exact: false }).selectOption(preset)
    if (!preset) await dialog.getByRole('spinbutton', { name: 'Nivel inicial', exact: true }).fill('85')
    current = await action('create', () => dialog.getByRole('button', { name: 'Preparar personaje', exact: true }).click())
    assert.equal(current.isNew, true); assert.equal(current.sourcePath, null)
    assert.equal(current.character.name, name); assert.equal(current.validation.roundtrip, true)
    return current
  }

  await page.goto(address.toString())
  await open(workingSave)
  const nativeFile = path.join(copyDirectory, `${current.character.name}.d2s`)
  if (nativeFile.toLowerCase() !== workingSave.toLowerCase()) { await fs.writeFile(nativeFile, sourceBytes, { flag: 'wx' }); workingSave = nativeFile; await open(workingSave) }
  checks.push('Private fixture opens with checksum and exact roundtrip; test copy uses its native character name')

  // Every operation below is a draft until the explicit disposable-copy save test.
  await tab('Equipo').click()
  const movable = current.items.find(entry => entry.page === 'inventory')
  assert.ok(movable, 'Fixture needs at least one inventory item')
  await item(movable.id)
  await page.getByLabel(/^Espacio/).selectOption('stash')
  const moved = await preview(() => button('Preparar traslado').click())
  assert.equal(moved.summary.items.find(entry => entry.id === movable.id)?.page, 'stash')
  assert.equal(await button('Guardar con copia').isDisabled(), true)
  await undo()
  assert.equal(current.items.find(entry => entry.id === movable.id)?.page, 'inventory')
  checks.push('Inventory move keeps item identity; manual review gates writes; undo restores native placement')
  const originalSet = current.activeWeaponSet
  const otherSet = originalSet === 1 ? 2 : 1
  await page.getByRole('tab', { name: new RegExp(`^Equipo ${otherSet === 1 ? 'I' : 'II'}(Activo)?$`) }).click()
  await preview(() => button(`Usar el equipo ${otherSet === 1 ? 'I' : 'II'}`).click())
  assert.equal(current.activeWeaponSet, otherSet)
  await undo(); assert.equal(current.activeWeaponSet, originalSet)
  checks.push('Weapon-set viewing and native active-set change are separate and reversible')
  for (const name of ['Baúl', 'Cubo', 'Cinturón', 'Inventario']) await tab(name).click()
  await capture('inventory-1440.png')
  await page.setViewportSize({ width: 1024, height: 900 })
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false)
  await capture('inventory-1024.png')
  await page.setViewportSize({ width: 1440, height: 1000 })
  checks.push('All four native storage pages render without horizontal overflow at1024px')

  await tab('Mercenario').click()
  await page.getByLabel(/^Tipo de mercenario/).waitFor()
  await page.waitForFunction(() => [...document.querySelectorAll('select')].some(select => select.closest('.mercenary-settings') && select.options.length > 1))
  if (current.mercenary?.exists) {
    assert.equal(await page.locator('.mercenary-equipment .item-name:is(button)').count(), current.mercenary.items.length)
    if (current.mercenary.items.some(entry => entry.twoHanded)) assert.equal(await page.locator('.mercenary-equipment .two-hand-ghost').count(), 1)
    const sameMerc = await preview(() => button('Preparar mercenario').click())
    assert.equal(sameMerc.summary.mercenary.typeId, current.mercenary.typeId)
    await undo()
    checks.push(`Mercenary native catalog and ${current.mercenary.items.length}-piece equipment render; two-hand ghost adds no item`)
  } else skipped.push('Mercenary equipment: fixture has no hired companion')
  await capture('mercenary.png')

  await tab('Mundo').click()
  await button('Transportadores').click()
  const waypoint = page.getByRole('checkbox', { name: /^Transportador / }).first()
  const wasActive = await waypoint.isChecked()
  await preview(() => waypoint.click())
  await undo()
  await page.waitForFunction(expected => [...document.querySelectorAll('input[aria-label^="Transportador "]')][0]?.checked === expected, wasActive)
  assert.equal(await waypoint.isChecked(), wasActive)
  await button('Misiones').click()
  const questIndex = await page.getByRole('combobox', { name: /^Estado de / }).evaluateAll(selects => selects.findIndex(select => select.value !== 'rewarded'))
  if (questIndex >= 0) {
    const quest = page.getByRole('combobox', { name: /^Estado de / }).nth(questIndex)
    const oldQuest = await quest.inputValue()
    await preview(() => quest.selectOption(oldQuest === 'started' ? 'notStarted' : 'started')); await undo()
    await page.waitForFunction(({ index, value }) => [...document.querySelectorAll('select[aria-label^="Estado de "]')][index]?.value === value, { index: questIndex, value: oldQuest })
  } else skipped.push('Quest transition: fixture has already claimed every quest reward')
  checks.push('Individual world waypoint and available quest changes roundtrip and undo through the real API')

  await tab('Personaje').click()
  const gold = current.character.attributes.gold
  await page.getByLabel(/^Oro en el personaje/).fill(String(gold + 1))
  await preview(() => page.locator('.save-actions').getByRole('button', { name: /Revisar/ }).click())
  if (!current.gameRunning) {
    workingRestored = false
    const committed = await action('commit', () => button('Guardar con copia').click())
    assert.equal(committed.summary.character.attributes.gold, gold + 1)
    assert.equal(digest(await fs.readFile(committed.backupPath)), originalHash)
    await button('Copias de seguridad').click()
    const backups = page.getByRole('dialog', { name: 'Copias de seguridad', exact: true })
    await action('restore', () => backups.getByRole('button', { name: 'Restaurar', exact: true }).first().click())
    assert.equal(digest(await fs.readFile(workingSave)), originalHash)
    workingRestored = true
    checks.push('Disposable working copy saves with an exact backup, then restores byte-identical through the UI')
  } else { skipped.push('Disposable commit/restore: Diablo II is running'); await undo() }

  await library()
  await page.getByRole('checkbox', { name: /^Sólo / }).uncheck()
  await page.waitForFunction(() => document.querySelectorAll('.preset-list-entry').length === 9)
  assert.equal(await page.locator('.preset-list-entry').count(), 9)
  await page.getByRole('heading', { name: 'Cómo jugarla', exact: true }).waitFor()
  await closeModal()
  checks.push('Nine native class templates expose human-readable guides for eight classes')

  await create('AkaraForge')
  checks.push('New-character wizard exposes all eight classes and creates a new level85 draft without a file')
  await tab('Taller').click()
  await page.getByLabel('Buscar base', { exact: true }).fill('amu')
  await page.getByRole('listbox', { name: 'Base del objeto', exact: true }).selectOption('amu')
  await page.getByLabel('Calidad', { exact: false }).selectOption('crafted')
  await page.getByLabel('Firma del artesano', { exact: false }).fill('QAForge')
  await page.getByRole('checkbox', { name: 'Usar una lista propia de propiedades', exact: true }).check()
  const properties = [['item_allskills', 2], ['item_fastercastrate', 20], ['fireresist', 30], ['lightresist', 30], ['coldresist', 30], ['poisonresist', 30]]
  for (const [name, value] of properties) {
    const catalog = await request(`catalog?sessionId=${current.sessionId}&type=stat&search=${name}&limit=250`)
    const stat = catalog.entries.find(entry => entry.name === name)
    assert.ok(stat, `Native stat ${name}`)
    await page.getByLabel('Buscar propiedad', { exact: true }).fill('')
    await page.getByRole('combobox', { name: 'Propiedad que añadir', exact: true }).selectOption(String(stat.nativeId))
    await button('Añadir propiedad').click()
    await page.locator('.custom-property').last().getByRole('spinbutton').first().fill(String(value))
  }
  await page.locator('.workshop-destination').getByLabel(/^Destino/).selectOption('amulet')
  const craftedPlan = await preview(() => button('Crear en la vista previa').click())
  const crafted = craftedPlan.summary.items.find(entry => entry.personalizedName === 'QAForge')
  assert.ok(crafted); assert.equal(crafted.stats.length, 6); assert.equal(crafted.slot, 'amulet'); assert.ok(['craft', 'crafted'].includes(crafted.quality))
  const blueprint = await download('Exportar plano del objeto', 'crafted.akara-item.json')
  assert.equal(blueprint.document.code, 'amu'); assert.equal(blueprint.document.description.personalizedName, 'QAForge')
  await page.getByLabel('Importar plano de objeto', { exact: true }).setInputFiles(blueprint.target)
  await page.locator('.workshop-destination').getByLabel(/^Destino/).selectOption('inventory')
  const importedPlan = await preview(() => button('Preparar importación').click())
  assert.equal(importedPlan.summary.items.filter(entry => entry.personalizedName === 'QAForge').length, 2)
  checks.push('Workshop creates a native crafted six-property signed amulet; exported blueprint imports without property loss')

  await tab('Equipo').click(); await item(crafted.id); await button('Editar en el taller').click()
  const castStat = crafted.stats.find(stat => stat.name === 'item_fastercastrate')
  const castInput = page.locator(`[data-stat-id="${castStat.id}"]`).getByRole('spinbutton').first()
  await castInput.fill('999999')
  const invalidDesign = await action('preview', () => button('Aplicar al objeto').click())
  assert.equal(invalidDesign.canSave, false)
  await page.locator('.workshop-layout').locator('..').getByRole('alert').first().waitFor()
  await castInput.fill('21')
  const edited = await preview(() => button('Aplicar al objeto').click())
  assert.equal(edited.summary.items.find(entry => entry.id === crafted.id)?.stats.find(stat => stat.name === 'item_fastercastrate')?.displayValue, 21)
  await undo()
  assert.equal(current.items.find(entry => entry.id === crafted.id)?.stats.find(stat => stat.name === 'item_fastercastrate')?.displayValue, 20)
  checks.push('Editing an existing item preserves its stable identity and supports undo')
  checks.push('An unencodable property is rejected before writing; the previous valid draft remains usable')
  await capture('workshop.png')

  await tab('Equipo').click()
  const importedItem = current.items.find(entry => entry.personalizedName === 'QAForge' && entry.id !== crafted.id)
  await item(importedItem.id); await button('Copiar').click(); await page.getByLabel(/^Espacio/).selectOption('stash')
  const beforeCopyCount = current.items.length
  const copied = await preview(() => button('Preparar copia').click())
  assert.equal(copied.summary.items.length, beforeCopyCount + 1)
  const copyId = copied.summary.items.find(entry => entry.page === 'stash' && entry.personalizedName === 'QAForge').id
  await page.getByRole('heading', { name: 'Equipo e inventario', exact: true }).click()
  await preview(() => page.keyboard.press('Control+z'))
  assert.equal(current.items.some(entry => entry.id === copyId), false)
  await preview(() => page.keyboard.press('Control+Shift+z'))
  assert.equal(current.items.some(entry => entry.id === copyId), true)
  await undo()
  await item(importedItem.id)
  await preview(() => button('Marcar para eliminar').click())
  assert.equal(current.items.some(entry => entry.id === importedItem.id), false)
  await undo()
  checks.push('Copy and delete remain reversible drafts; Ctrl+Z and Ctrl+Shift+Z replay native validated operations')

  await tab('Taller').click(); await button('Crear una versión nueva').click()
  await page.getByLabel('Buscar base', { exact: true }).fill('cap')
  await page.getByRole('listbox', { name: 'Base del objeto', exact: true }).selectOption('cap')
  await page.getByLabel('Calidad', { exact: false }).selectOption('normal')
  await page.getByRole('checkbox', { name: 'Usar una lista propia de propiedades', exact: true }).uncheck()
  await page.getByLabel('Firma del artesano', { exact: false }).fill('QASocket')
  await page.getByRole('spinbutton', { name: 'Número de huecos', exact: true }).fill('1')
  await page.getByLabel('Hueco 1', { exact: false }).selectOption('r22')
  await page.locator('.workshop-destination').getByLabel(/^Destino/).selectOption('inventory')
  await preview(() => button('Crear en la vista previa').click())
  const socketed = current.items.find(entry => entry.personalizedName === 'QASocket')
  assert.equal(socketed.sockets.length, 1); assert.equal(socketed.sockets[0].code, 'r22')
  await tab('Equipo').click(); await item(socketed.id); await button('Editar en el taller').click()
  await page.getByRole('spinbutton', { name: 'Número de huecos', exact: true }).fill('0')
  await preview(() => button('Aplicar al objeto').click())
  assert.equal(current.items.find(entry => entry.id === socketed.id).sockets.length, 0)
  await undo()
  assert.equal(current.items.find(entry => entry.id === socketed.id).sockets[0].code, 'r22')
  checks.push('Native socket child creation, explicit socket removal and undo preserve parent identity')

  await preview(() => page.locator('.save-actions').getByRole('button', { name: /Revisar/ }).click())
  const exported = await action('export', () => button('Exportar copia').click())
  assert.equal(inSavedGames(exported.outputPath), false)
  assert.ok((await fs.stat(exported.outputPath)).size > 0)
  await button('Copiar ruta').click()
  assert.equal(await page.evaluate(() => navigator.clipboard.readText()), exported.outputPath)
  checks.push('Reviewed draft exports a real independent .d2s; persistent result copies the actual output path')

  await library(); await tab('Mi biblioteca').click()
  const libraryName = `Browser QA ${path.basename(output)}`
  await page.getByRole('textbox', { name: 'Nombre de la configuración', exact: true }).fill(libraryName)
  const stored = await action('loadouts/save', () => button('Guardar en la biblioteca').click()); libraryIds.add(stored.id)
  await page.getByRole('button', { name: new RegExp(`^${libraryName}`) }).click()
  const loadout = await download('Exportar configuración', 'build.akara-loadout.json')
  assert.equal(loadout.document.entries.length, 1)
  assert.equal(loadout.document.entries[0].blueprint.description.personalizedName, 'QAForge')
  const importedLoadout = await action('loadouts/import', () => page.getByLabel('Importar configuración de equipo', { exact: true }).setInputFiles(loadout.target))
  libraryIds.add(importedLoadout.id)
  await page.getByText('Configuración importada y validada con el perfil actual.', { exact: true }).waitFor()
  await preview(() => button('Preparar configuración').click())
  assert.equal(current.items.filter(entry => entry.slot === 'amulet' && entry.page === 'equipped').length, 1)
  await undo()
  checks.push('Local loadout library saves, exports, validates an import, applies it and restores the prior draft with undo')

  await button('Clonar personaje').click()
  await page.getByLabel('Nombre del nuevo personaje', { exact: false }).fill('AkaraClone')
  const cloned = await action('clone', () => page.getByRole('dialog').getByRole('button', { name: 'Preparar copia', exact: true }).click())
  assert.equal(cloned.isNew, true); assert.equal(cloned.sourcePath, null); assert.equal(cloned.character.name, 'AkaraClone')
  assert.equal(cloned.items.length, current.items.length)
  current = cloned
  checks.push('Clone wizard keeps the validated equipment in an independent in-memory character')

  await create('AkaraHybrid', 'squad-sorceress')
  assert.equal(current.character.level, 85)
  assert.equal(current.mercenary.items.length, 9)
  assert.ok(current.items.some(entry => entry.weaponSet === 2))
  await preview(() => page.locator('.save-actions').getByRole('button', { name: /Revisar/ }).click())
  assert.equal(await button('Exportar copia').isEnabled(), true)
  checks.push('Hybrid Sorceress template creates a validated level85 draft with weapon setII and nine-piece mercenary')
  if (!current.gameRunning) {
    await button('Guardar con copia').click()
    const destination = path.join(output, 'AkaraHybrid.d2s')
    await page.getByLabel('Archivo de destino', { exact: false }).fill(destination)
    const created = await action('commit', () => button('Crear archivo').click())
    assert.equal(created.summary.isNew, false); assert.equal(created.outputPath.toLowerCase(), destination.toLowerCase())
    assert.equal(created.summary.validation.roundtrip, true)
    current = created.summary
    checks.push('New-character destination dialog creates a validated file only in the private test output directory')
  } else skipped.push('New-character commit: Diablo II is running')
  await tab('Habilidades').click()
  await capture('skills.png')
  await tab('Personaje').click()
  await page.setViewportSize({ width: 1024, height: 900 })
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false)
  await capture('character-1024.png')
  await library(); await tab('Mi biblioteca').click()
  while (libraryIds.size) {
    const id = [...libraryIds][0]
    const entries = await request('loadouts')
    const entry = entries.entries.find(candidate => candidate.id === id)
    if (!entry) { libraryIds.delete(id); continue }
    await page.locator('.preset-list-entry').filter({ hasText: entry.name }).first().click()
    // Observe the ID selected by the UI; duplicate names must never select an unrelated entry.
    const [removed] = await Promise.all([page.waitForResponse(response => new URL(response.url()).pathname.startsWith('/api/loadouts/') && response.request().method() === 'DELETE'), button('Eliminar de la biblioteca').click()])
    assert.equal(removed.status(), 200)
    assert.ok(libraryIds.has(decodeURIComponent(new URL(removed.url()).pathname.split('/').at(-1))))
    const remaining = await request('loadouts')
    for (const candidate of [...libraryIds]) if (!remaining.entries.some(value => value.id === candidate)) libraryIds.delete(candidate)
  }
  await closeModal()
  assert.equal(libraryIds.size, 0, 'Test library entries must be removed through the UI')
  await library()
  await page.keyboard.press('Escape')
  assert.equal(await page.getByRole('dialog').count(), 0)
  assert.equal(await button('Ver plantillas y equipos').evaluate(element => element === document.activeElement), true)
  await tab('Personaje').focus(); await page.keyboard.press('ArrowRight')
  assert.equal(await tab('Habilidades').getAttribute('aria-selected'), 'true')
  checks.push('Test-only library entries are removed; Escape restores modal focus and arrow keys navigate editor tabs')
  assert.deepEqual(errors, [])
  assert.equal(digest(await fs.readFile(source)), originalHash)
  assert.equal(digest(await fs.readFile(workingSave)), originalHash)
  assert.equal(workingRestored, true)
  checks.push('Supplied fixture and restored disposable source remain byte-identical; no live-save write was performed')
  const report = { passed: true, checks, skipped, pageErrors: errors, liveWritesPerformed: false, sourceFixtureUnchanged: true }
  await fs.writeFile(path.join(output, 'report.json'), JSON.stringify(report, null, 2))
  console.log(JSON.stringify(report, null, 2))
} catch (error) {
  await page?.screenshot({ path: path.join(output, 'failure.png'), fullPage: true }).catch(() => undefined)
  const report = { passed: false, checks, skipped, error: safeError(error), pageErrors: errors, liveWritesPerformed: false, testLibraryEntryIds: [...libraryIds], disposableWorkingCopyRestored: workingRestored }
  await fs.writeFile(path.join(output, 'report.json'), JSON.stringify(report, null, 2))
  console.error(JSON.stringify(report, null, 2)); process.exitCode = 1
} finally { await browser?.close() }
