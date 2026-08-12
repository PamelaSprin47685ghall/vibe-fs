// tests/unit/support/glory.mjs — GLORY frozen-text facade (SURFACE-004).
//
// Tests never copy frozen text; they read the owner modules (Domain owners)
// and the packaged resources. A renamed Fable export fails loudly at load
// instead of reading `undefined` (VERIFY-008).

import { readFileSync } from 'node:fs'
import { fileURLToPath, pathToFileURL } from 'node:url'
import path from 'node:path'

const here = path.dirname(fileURLToPath(import.meta.url))
const root = path.join(here, '..', '..', '..')
const dist = (rel) => pathToFileURL(path.join(root, 'dist', rel)).href

const load = async (rel, names) => {
  const module = await import(dist(rel))
  const out = {}
  for (const name of names) {
    const value = module[name] ?? module[`${name}`]
    const exported = module[`${name}`] ?? module[Object.keys(module).find((k) => k.endsWith(`_${name}`))]
    if (exported === undefined) throw new Error(`missing export ${name} in ${rel}`)
    out[name] = exported
  }
  return out
}

const narrative = await load('Domain/ManagerNarrative.js', [
  'PlanningTail',
  'ReawakeningPrefix',
  'planningTableDocument',
  't1RevelationDocument',
  'wrapT1AcceptedResult',
  'firstBirth',
  'reawakening',
  'renderText',
])
const lifecycle = await load('Domain/ManagerLifecyclePrompt.js', [
  'WorkActivation',
  'IdleEncouragementPreT1',
  'IdleEncouragementPostT1',
  'FinalityUndecidable',
])
const finality = await load('Domain/FinalityPrompt.js', ['rejected', 'blessed', 'rest'])
const hostReview = await load('Domain/HostReviewPrompt.js', ['OpeningAssignment'])

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

export const managerNarrative = {
  planningTail: () => narrative.PlanningTail,
  reawakeningPrefix: () => narrative.ReawakeningPrefix,
  planningTableDocument: () => narrative.planningTableDocument,
  t1RevelationDocument: () => narrative.t1RevelationDocument,
  wrapT1AcceptedResult: (body) => narrative.wrapT1AcceptedResult(body),
  firstBirth: (text) => projectionView(narrative.firstBirth(text)),
  reawakening: (text) => projectionView(narrative.reawakening(text)),
  firstBirthText: (text) => narrative.renderText(narrative.firstBirth(text)),
  reawakeningText: (text) => narrative.renderText(narrative.reawakening(text)),
}

export const managerLifecyclePrompt = {
  workActivation: () => lifecycle.WorkActivation,
  idleEncouragementPreT1: () => lifecycle.IdleEncouragementPreT1,
  idleEncouragementPostT1: () => lifecycle.IdleEncouragementPostT1,
  finalityUndecidable: () => lifecycle.FinalityUndecidable,
}

export const finalityPrompt = {
  rejected: (record) => finality.rejected(record),
  blessed: (record) => finality.blessed(record),
  rest: () => finality.rest,
}

export const hostReviewPrompt = {
  openingAssignment: () => hostReview.OpeningAssignment,
}

const resourcesDir = path.join(root, 'resources', 'prompts')
export const managerSystemPrompt = () => readFileSync(path.join(resourcesDir, 'manager-system.md'), 'utf8')
export const reviewerSystemPrompt = () => readFileSync(path.join(resourcesDir, 'reviewer-system.md'), 'utf8')
