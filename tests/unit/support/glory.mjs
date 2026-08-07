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

const narrative = await load('Domain/ManagerNarrative.js', ['firstBirth', 'reawakening'])
const lifecycle = await load('Domain/ManagerLifecyclePrompt.js', ['WorkActivation', 'IdleEncouragement', 'FinalityUndecidable'])
const finality = await load('Domain/FinalityPrompt.js', ['rejected'])
const hostReview = await load('Domain/HostReviewPrompt.js', ['OpeningAssignment'])

export const managerNarrative = {
  firstBirth: (text) => narrative.firstBirth(text),
  reawakening: (text) => narrative.reawakening(text),
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
