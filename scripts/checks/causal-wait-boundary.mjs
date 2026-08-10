#!/usr/bin/env node
/**
 * Causal wait boundary gate (causal-ce-observability Phase 8).
 *
 * Proves only what static analysis can reliably prove — not "every wait is observed".
 *
 * 1. Domain must not reference CausalWaitRegistry / CausalWaitHub implementation
 * 2. Application must not access IWaitSnapshotReader
 * 3. CausalWait must not enter Fact / Journal codec surfaces
 * 4. diagnostics snapshot must not enter PromptDispatcher / decision paths
 * 5. Critical migrated sites must not reintroduce bare TCS.Task awaits
 * 6. CausalWaitRegistry mutable fields must carry DSL-MUTABLE annotations
 */

import { readFileSync } from 'node:fs'
import { resolve, dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const src = join(root, 'src/Wanxiangshu')
const norm = (p) => p.replace(/\\/g, '/')

const problems = []

const read = (abs) => readFileSync(abs, 'utf8')

const filesUnder = (relDir) =>
  [...walk(join(src, relDir))].filter((f) => f.endsWith('.fs')).map((f) => ({ abs: f, rel: norm(f.slice(src.length + 1)) }))

const allFs = () =>
  [...walk(src)].filter((f) => f.endsWith('.fs')).map((f) => ({ abs: f, rel: norm(f.slice(src.length + 1)) }))

// 1. Domain ↛ CausalWaitRegistry / CausalWaitHub / CausalAwait
for (const { abs, rel } of filesUnder('Domain')) {
  const text = read(abs)
  if (/CausalWait(?:Registry|Hub)|CausalAwait/.test(text)) {
    problems.push(`Domain/${rel}: must not reference CausalWaitRegistry/Hub/Await`)
  }
}

// 2. Application ↛ IWaitSnapshotReader
for (const { abs, rel } of filesUnder('Application')) {
  const text = read(abs)
  if (/IWaitSnapshotReader/.test(text)) {
    problems.push(`Application/${rel}: must not access IWaitSnapshotReader`)
  }
}

// 3. CausalWait not in Fact / Journal codec
for (const { abs, rel } of allFs()) {
  const parts = rel.split('/')
  const underJournal = parts.includes('Journal')
  const isFact = parts.length >= 2 && parts.at(-2) === 'Kernel' && parts.at(-1) === 'Fact.fs'
  if (!underJournal && !isFact) continue
  const body = read(abs)
  if (/CausalWait|WaitKind|IWaitSnapshotReader|CausalAwait/.test(body)) {
    problems.push(`${rel}: CausalWait must not enter Fact/Journal codec surfaces`)
  }
}

// 4. diagnostics snapshot not in PromptDispatcher / decision
for (const name of ['Session/PromptDispatcher.fs', 'Application/Reconciliation/TurnCompletionProgram.fs']) {
  const abs = join(src, name)
  try {
    const text = read(abs)
    if (/IWaitSnapshotReader|CausalWaitHub\.(?:snapshot|read)|causal-waits\.json/.test(text)) {
      problems.push(`${name}: diagnostics snapshot must not enter decision/prompt paths`)
    }
  } catch {
    /* optional path */
  }
}

// 5. Critical migrated sites — no bare `return! xxx.Task` / `do! xxx.Task` outside CausalAwait lines
const critical = [
  'Session/SyncDelegateRuntime.fs',
  'Application/Finality/CohortWorkflow.fs',
  'Infrastructure/OpenCode/Tools/FinalityTool.fs',
  'Infrastructure/OpenCode/Tools/JoinTool.fs',
  'Infrastructure/OpenCode/Orchestration/Host.fs',
  'Application/Review/ReviewBarrierWorkflow.fs',
  'Application/Orchestration/ManagerJob.fs',
]

for (const rel of critical) {
  const abs = join(src, rel)
  const text = read(abs)
  const lines = text.split('\n')
  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i]
    if (!/\b(return!|do!)\s+\w[\w.]*\.Task\b/.test(line)) continue
    // CancelToken.Task arms inside Promise.race / CausalAwait escapes are not
    // a reintroduced business wait — the outer race is what CausalAwait wraps.
    if (/\bcancel\.Task\b/.test(line)) continue
    // Allow when CausalAwait.await appears in a nearby window (argument form).
    const window = lines.slice(Math.max(0, i - 40), i + 1).join('\n')
    if (/CausalAwait\.await/.test(window)) continue
    // Local fan-in helpers (concurrentAll*) settle a private TCS; callers own
    // the observed wait descriptors on each branch task.
    const ahead = lines.slice(Math.max(0, i - 80), i).join('\n')
    if (/let private concurrent/.test(ahead) && /return! tcs\.Task/.test(line)) continue
    problems.push(`${rel}:${i + 1}: bare TCS.Task await outside CausalAwait (${line.trim()})`)
  }
}

// 6. CausalWaitRegistry mutable must be DSL-MUTABLE annotated
{
  const abs = join(src, 'Session/CausalWaitRegistry.fs')
  const text = read(abs)
  const lines = text.split('\n')
  for (let i = 0; i < lines.length; i += 1) {
    if (!/\blet mutable\b/.test(lines[i])) continue
    const prev = lines.slice(Math.max(0, i - 2), i).join('\n')
    if (!/\/\/\s*DSL-MUTABLE:/.test(prev)) {
      problems.push(`Session/CausalWaitRegistry.fs:${i + 1}: mutable lacks DSL-MUTABLE annotation`)
    }
  }
}

if (problems.length > 0) {
  console.error('causal-wait-boundary FAILED:')
  for (const p of problems) console.error(`  - ${p}`)
  process.exit(1)
}

console.log('causal-wait-boundary OK')
