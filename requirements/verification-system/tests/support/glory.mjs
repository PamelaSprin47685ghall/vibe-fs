// tests/unit/support/glory.mjs — GLORY frozen-text facade (SURFACE-004).
//
// Tests never copy frozen text; they read Domain assemblers + provider resources.
// A renamed Fable export fails loudly at load instead of reading `undefined` (VERIFY-008).

import { readFileSync } from 'node:fs'
import { fileURLToPath, pathToFileURL } from 'node:url'
import path from 'node:path'

const here = path.dirname(fileURLToPath(import.meta.url))
// requirements/verification-system/tests/support → repo root (4 levels up).
const root = path.join(here, '..', '..', '..', '..')
const dist = (rel) => pathToFileURL(path.join(root, 'dist', rel)).href

const load = async (rel, names) => {
  const module = await import(dist(rel))
  const out = {}
  for (const name of names) {
    const exported = module[`${name}`] ?? module[Object.keys(module).find((k) => k.endsWith(`_${name}`))]
    if (exported === undefined) throw new Error(`missing export ${name} in ${rel}`)
    out[name] = exported
  }
  return out
}

/** Instruction-only SyntheticToml.document layout (ARCH-010): empty line → `#`, else `# ` + line. */
const syntheticDocument = (body) => {
  const normalized = String(body).replace(/\r\n/g, '\n').replace(/\r/g, '\n').trimEnd()
  return normalized.split('\n').map((line) => (line === '' ? '#' : `# ${line}`)).join('\n') + '\n'
}

const readProviderRaw = (semanticPath) =>
  readFileSync(path.join(root, 'resources', 'provider', semanticPath, 'en.md'), 'utf8')
    .replace(/\r\n/g, '\n')
    .replace(/\r/g, '\n')
    .trim()

const readProviderDocument = (semanticPath) => syntheticDocument(readProviderRaw(semanticPath))

const narrative = await load('Domain/ManagerNarrative.js', [
  'wrapT1AcceptedResult',
  'firstBirth',
  'reawakening',
  'renderText',
])
const finality = await load('Domain/FinalityPrompt.js', ['rejected', 'blessed'])

/** F# list → array (Fable list has head/tail). */
const listToArray = (list) => {
  const out = []
  let cur = list
  while (cur && cur.tail !== undefined) {
    if (cur.head !== undefined) out.push(cur.head)
    cur = cur.tail
  }
  return out
}

const projectionView = (projection) => {
  const parts = listToArray(projection.Parts).map((part) => ({
    text: part.Text,
    synthetic: Boolean(part.Synthetic),
  }))
  return {
    parts,
    text: narrative.renderText(projection),
  }
}

const planningTable = () => readProviderDocument('lifecycle/manager/planning-table')
const t1Revelation = () => readProviderDocument('lifecycle/manager/t1-revelation')
const reawakeningDoc = () => readProviderDocument('lifecycle/manager/reawakening')

export const managerNarrative = {
  planningTail: () => readProviderRaw('lifecycle/manager/planning-tail'),
  reawakeningPrefix: () => readProviderRaw('lifecycle/manager/reawakening').split('\n')[0],
  planningTableDocument: planningTable,
  t1RevelationDocument: t1Revelation,
  wrapT1AcceptedResult: (body) => narrative.wrapT1AcceptedResult(t1Revelation(), body),
  firstBirth: (text) => projectionView(narrative.firstBirth(text, planningTable())),
  reawakening: (text) => projectionView(narrative.reawakening(text, reawakeningDoc(), planningTable())),
  firstBirthText: (text) => narrative.renderText(narrative.firstBirth(text, planningTable())),
  reawakeningText: (text) =>
    narrative.renderText(narrative.reawakening(text, reawakeningDoc(), planningTable())),
}

export const managerLifecyclePrompt = {
  workActivation: () => readProviderDocument('lifecycle/manager/work-activation'),
  idleEncouragementPreT1: () => readProviderDocument('lifecycle/manager/idle-pre-t1'),
  idleEncouragementPostT1: () => readProviderDocument('lifecycle/manager/idle-post-t1'),
  finalityUndecidable: () => readProviderDocument('lifecycle/manager/finality-undecidable'),
}

export const finalityPrompt = {
  rejected: (record) =>
    finality.rejected(readProviderDocument('lifecycle/finality/rejected'), record),
  blessed: (record) => finality.blessed(readProviderDocument('lifecycle/finality/blessed'), record),
  rest: () => readProviderDocument('lifecycle/finality/rest'),
}

export const hostReviewPrompt = {
  openingAssignment: () => readProviderDocument('lifecycle/host-review/opening'),
}

export const managerSystemPrompt = () =>
  readFileSync(path.join(root, 'resources', 'provider', 'role', 'manager', 'en.md'), 'utf8')
export const reviewerSystemPrompt = () =>
  readFileSync(path.join(root, 'resources', 'provider', 'role', 'reviewer', 'en.md'), 'utf8')
