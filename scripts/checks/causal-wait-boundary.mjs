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

import { existsSync, readFileSync } from 'node:fs'
import { resolve, dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const src = join(root, 'src/Wanxiangshu')
const norm = (p) => p.replace(/\\/g, '/')

const problems = []

const read = (abs) => readFileSync(abs, 'utf8')

const filesUnder = (relDir) => {
  const dir = join(src, relDir)
  if (!existsSync(dir)) return []
  return [...walk(dir)].filter((f) => f.endsWith('.fs')).map((f) => ({ abs: f, rel: norm(f.slice(src.length + 1)) }))
}

const allFs = () =>
  [...walk(src)].filter((f) => f.endsWith('.fs')).map((f) => ({ abs: f, rel: norm(f.slice(src.length + 1)) }))

// 1. Pure domain layers must not reference CausalWaitRegistry implementation / snapshot readers
const domainLayers = ['Foundation', 'Participant', 'Interaction', 'Mission', 'Strength', 'Context', 'Repository']
for (const layer of domainLayers) {
  for (const { abs, rel } of filesUnder(layer)) {
    if (rel.includes('/OpenCode/') || rel.includes('/Host/') || rel.endsWith('Surface.fs') || rel.includes('BookkeeperRuntime.fs')) continue
    const text = read(abs)
    if (/CausalWaitRegistry|CausalWaitHub\.(?:snapshot|read)/.test(text)) {
      problems.push(`Domain/${rel}: must not reference CausalWaitRegistry/snapshot`)
    }
  }
}

// 2. Application ↛ IWaitSnapshotReader
const appLayers = ['Execution', 'Mission', 'Change', 'Composition']
for (const layer of appLayers) {
  for (const { abs, rel } of filesUnder(layer)) {
    if (rel.includes('/Wait/')) continue
    const text = read(abs)
    if (/IWaitSnapshotReader/.test(text)) {
      problems.push(`Application/${rel}: must not access IWaitSnapshotReader`)
    }
  }
}

// 3. CausalWait not in Fact / Journal codec
for (const { abs, rel } of allFs()) {
  const parts = rel.split('/')
  const underJournal = parts.includes('Journal')
  const isFact = parts.at(-1)?.endsWith('Fact.fs') || parts.at(-1)?.endsWith('Facts.fs')
  if (!underJournal && !isFact) continue
  const body = read(abs)
  if (/CausalWaitRegistry|IWaitSnapshotReader/.test(body)) {
    problems.push(`${rel}: CausalWait must not enter Fact/Journal codec surfaces`)
  }
}

// 4. diagnostics snapshot not in PromptDispatcher / decision
for (const name of ['Interaction/Dispatch/Dispatcher.fs', 'Composition/Turn/TurnReconcile.fs', 'Mission/Manager/Workflow.fs']) {
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
  'Execution/Delegation/SyncDelegate/Workflow.fs',
  'Mission/Finality/Cohort.fs',
  'Mission/Finality/OpenCode/Tool.fs',
  'Execution/Delegation/Fork/OpenCode/JoinTool.fs',
  'Change/Host/Host.fs',
  'Mission/Review/Barrier/Workflow.fs',
  'Change/Job.fs',
]

for (const rel of critical) {
  const abs = join(src, rel)
  if (!existsSync(abs)) {
    problems.push(`${rel}: critical causal-wait site missing on disk`)
    continue
  }
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
  const abs = join(src, 'Execution/Session/Wait/Registry.fs')
  if (!existsSync(abs)) {
    problems.push('Execution/Session/Wait/Registry.fs: missing on disk')
  } else {
    const text = read(abs)
    const lines = text.split('\n')
    for (let i = 0; i < lines.length; i += 1) {
      if (!/\blet mutable\b/.test(lines[i])) continue
      const prev = lines.slice(Math.max(0, i - 2), i).join('\n')
      if (!/\/\/\s*DSL-MUTABLE:/.test(prev)) {
        problems.push(`Execution/Session/Wait/Registry.fs:${i + 1}: mutable lacks DSL-MUTABLE annotation`)
      }
    }
  }
}

if (problems.length > 0) {
  console.error('causal-wait-boundary FAILED:')
  for (const p of problems) console.error(`  - ${p}`)
  process.exit(1)
}

console.log('causal-wait-boundary OK')
