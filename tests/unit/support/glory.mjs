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
    // Fable emits `Module_export`; fall back to the bare name.
    const exported = module[`${name}`] ?? module[Object.keys(module).find((k) => k.endsWith(`_${name}`))]
    if (exported === undefined) throw new Error(`missing export ${name} in ${rel}`)
    out[name] = exported
  }
  return out
}

const narrative = await load('Domain/ManagerNarrative.js', [
  'PlanningTail',
  'ReawakeningPrefix',
  'firstBirth',
  'reawakening',
  'renderText',
])
const lifecycle = await load('Domain/ManagerLifecyclePrompt.js', ['WorkActivation', 'IdleEncouragement', 'FinalityUndecidable'])
const finality = await load('Domain/FinalityPrompt.js', ['rejected'])
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
  firstBirth: (text) => projectionView(narrative.firstBirth(text)),
  reawakening: (text) => projectionView(narrative.reawakening(text)),
  /** Joined text view for LF / golden compatibility. */
  firstBirthText: (text) => narrative.renderText(narrative.firstBirth(text)),
  reawakeningText: (text) => narrative.renderText(narrative.reawakening(text)),
}

export const managerLifecyclePrompt = {
  workActivation: () => lifecycle.WorkActivation,
  idleEncouragement: () => lifecycle.IdleEncouragement,
  finalityUndecidable: () => lifecycle.FinalityUndecidable,
}

export const finalityPrompt = {
  rejected: (record) => finality.rejected(record),
}

export const hostReviewPrompt = {
  openingAssignment: () => hostReview.OpeningAssignment,
}

// Packaged system prompts (the resource owners).
const resourcesDir = path.join(root, 'resources', 'prompts')
export const managerSystemPrompt = () => readFileSync(path.join(resourcesDir, 'manager-system.md'), 'utf8')
export const reviewerSystemPrompt = () => readFileSync(path.join(resourcesDir, 'reviewer-system.md'), 'utf8')
