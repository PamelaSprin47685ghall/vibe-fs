#!/usr/bin/env node
// DSL ownership gate (VERIFY-001 layer 0).
// Checks that Program/Domain files do not contain forbidden control-flow escape hatches.
//
// Modes:
//   node scripts/checks/dsl-ownership.mjs                  fail-closed on any violation
//   node scripts/checks/dsl-ownership.mjs --threshold=N    fail-closed on violations > N
//
// CI calls with --threshold to freeze the current backlog while preventing new violations.

import { readFileSync } from 'node:fs'
import { relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const PRODUCTION_ROOT = 'src/Wanxiangshu'
export const PROGRAM_DIRS = [
  `${PRODUCTION_ROOT}/Agent/`,
  `${PRODUCTION_ROOT}/Application/`,
  `${PRODUCTION_ROOT}/Domain/`,
  `${PRODUCTION_ROOT}/Kernel/`,
  `${PRODUCTION_ROOT}/Process/`,
  `${PRODUCTION_ROOT}/Session/`,
]

const norm = (p) => p.replace(/\\/g, '/')

/**
 * External-protocol interpreters allowed by FLOW-006 (JSON/TOML/Host-wire
 * codec/parser/interpreter). Boundary is path+semantic, not a blanket `Interpreter`
 * suffix exemption: a `*Interpreter` module is tolerated only when it interprets an
 * external data format, signalled by a Codec/Parser/Wire path segment or suffix.
 */
export const isExternalProtocolPath = (file) => {
  const rel = norm(String(file))
  return (
    /\/Codec\//.test(rel) ||
    /\/Parser\//.test(rel) ||
    /\/Wire\//.test(rel) ||
    /\.(?:Codec|Parser|Wire)\.fs$/.test(rel) ||
    /\.(?:Codec|Parser|Wire)\.[A-Za-z]/.test(rel)
  )
}

/** Process/ is the PTY/process physical layer; its mutable fields and wire
 *  command DUs are runtime resources / external protocol messages, not business
 *  program counters. */
export const isProcessPhysicalPath = (file) => {
  const rel = norm(String(file))
  return rel.includes('/Process/')
}

/** Pty/Node protocol command types are external protocol messages (FLOW-006),
 *  not a business Command/Reply second runtime. */
export const isProcessCommandPath = (file) => {
  const rel = norm(String(file))
  return /\/Process\/(?:ProcessRequest|PtyTypes)\.fs$/.test(rel)
}

/**
 * Legal mutable (FLOW):
 * - Domain pure algorithm scratch
 * - Kernel/Parallel bounded concurrency cells
 * - Session / Application physical runtime cells (maps, single-flight, create tasks, locks)
 * Agent and non-Parallel Kernel remain fail-closed on `let mutable`.
 */
export const isMutableScratchPath = (file) => {
  const rel = String(file).replace(/\\/g, '/')
  return (
    rel.includes('/Domain/') ||
    rel.includes('/Session/') ||
    rel.includes('/Application/') ||
    /(?:^|\/)Kernel\/Parallel\.fs$/.test(rel)
  )
}


/**
 * Host-facing Session adapters may open OpenCode/Process/Infrastructure.
 * Basename allowlist only — other Session/Application files stay fail-closed.
 * These files compose Host ports / PromptDispatcher extensions / Pty backends.
 */
export const HOST_BOUNDARY_OPEN_BASENAMES = new Set([
  'CompanionHost.fs',
  'CompanionHostBlogger.fs',
  'HostForkAgent.fs',
  'HostForkAgentOwner.fs',
  'HostForkBusyNudge.fs',
  'HostForkChildDispatch.fs',
  'HostForkPty.fs',
  'HostForkRestart.fs',
  'HostForkRunLifecycle.fs',
  'HostForkRuntime.fs',
])

export const isHostBoundaryOpenPath = (file) => {
  const base = String(file).replace(/\\/g, '/').split('/').pop()
  return HOST_BOUNDARY_OPEN_BASENAMES.has(base)
}

export const FORBIDDEN = [
  { gate: 'mutable', pattern: /(?<!\/\/\s*)\blet mutable\b/, label: 'let mutable', skipIf: (file) => isMutableScratchPath(file) || isProcessPhysicalPath(file) },
  { gate: 'flow-lift', pattern: /\bFlow\.(?:lift|create)\b/, label: 'Flow.lift / Flow.create' },
  {
    // FLOW-002/FLOW-006 second-runtime forms. Catches realistic bypass shapes:
    //   type|and + optional private/internal/public modifier + *Command|*Reply
    //   (with optional generic params) or *Program (generic only, so a plain
    //   `type OrchestratorProgramDeps =` record stays clean), a `| Step of` /
    //   `| Suspend of` union node, and a ProtocolMismatch compensation token.
    gate: 'second-runtime-protocol',
    pattern:
      /\b(?:type|and)\s+(?:private\s+|internal\s+|public\s+)?(?:\w*(?:Command|Reply)(?:<[^=>]*>)?|(?:\w*Program)<[^=>]*>)\s*=|\|\s*(?:Step|Suspend)\s+of\b|\bProtocolMismatch\b/,
    label: 'Command/Reply/Program AST, Step/Suspend node, or ProtocolMismatch compensation',
    skipIf: isProcessCommandPath,
  },
  {
    // FLOW-006 internal business Interpreter. Allows private/internal/top-level
    // forms, but never a legitimate external-protocol interpreter (FLOW-006:
    // JSON/TOML/Host-wire codec/parser/interpreter are not second-runtime).
    gate: 'business-interpreter',
    pattern: /\bmodule\s+(?:private\s+|internal\s+)?(?:\w+\.)*\w*Interpreter\s*=/,
    label: 'internal business Interpreter module',
    skipIf: isExternalProtocolPath,
  },
  {
    gate: 'infrastructure-leak',
    pattern:
      /\b(?:open Wanxiangshu\.Infrastructure|open Wanxiangshu\.OpenCode|open Wanxiangshu\.Process)\b/,
    label: 'infrastructure namespace open',
    skipIf: (file) => isHostBoundaryOpenPath(file) || isProcessPhysicalPath(file),
  },
  {
    gate: 'program-counter',
    pattern:
      /\b(?:Dirty|Running|RepairSpent|ReactivatedAfterSeal|injectRepair|commitUnknown|abandonThenCatchUp|forceConfirmedReviewer|isContinuation|publishToMailbox|openReviewBarrier|CurrentStage|CurrentMode|RuntimeCondition|LifecyclePosition|InFlightFlag|ParkedMarker)\b/,
    label: 'program counter field/parameter',
  },
  {
    gate: 'behaviour-bool',
    // Domain evidence DUs / pure queries ending in Pending|Spent|Phase are allowlisted.
    // Program-counter bools and staging slots stay forbidden by exact name or residual suffix.
    pattern:
      /\b(?!TddPhase\b|parseTddPhase\b|UnknownTddPhase\b|PerfectPending\b|isPerfectPending\b|StillPending\b|ConflictPending\b|recoveryBudgetSpent\b|tryTakePending\b)[a-zA-Z]+(?:Stage|Phase|Next|Running|Pending|Spent|Already|Should)\b|\b(HasPendingCompletion|LastCompletionStatus|bloggerTask|bloggerFailed)\b/,
    label: 'behaviour bool or stage field',
    skipIf: isProcessPhysicalPath,
  },
  {
    // PR 9 item 4: multi-bool loop is a FILE-level pattern (two mutable false
    // booleans + a while loop), enforced in scanFiles. Registered here so
    // GATE_NAMES / counts stay authoritative.
    gate: 'bool-loop',
    pattern: /$^/,
    label: 'multi mutable-false booleans with a while loop',
  },
  {
    // PR 9 item 6: duplicate case sets across DUs are a FILE-level pattern
    // (two DUs with the exact same case-name set), enforced in scanFiles.
    // Registered here so GATE_NAMES / counts stay authoritative.
    gate: 'dup-cases',
    pattern: /$^/,
    label: 'duplicate DU case-name set',
  },
]

// PR 9 item 6 exemptions — each entry names the FILE:DU whose case set may
// legitimately repeat another DU's, with the reason the pair is not a
// duplicate knowledge representation:
//   ChildRecovery.fs:ChildResolution    pure Decision layer (no payloads),
//                                       1:1 to ChildRecoveryResult after effects
//   ManagedAgent.fs:ManagedAgentParseError  Infrastructure boundary; one-way
//                                       from AgentNameRejection (ManagedAgent.fs:66)
export const DUP_CASES_EXEMPT = new Set([
  'ChildRecovery.fs:ChildResolution',
  'ManagedAgent.fs:ManagedAgentParseError',
])

export const GATE_NAMES = FORBIDDEN.map((item) => item.gate)

export const isProgramFile = (path) => {
  const rel = norm(relative('.', path))
  return PROGRAM_DIRS.some((dir) => rel.startsWith(dir)) && rel.endsWith('.fs')
}

/** Scan one source text. Returns [{gate, file, line, text}, ...]. */
export const scanText = (text, file = '<synthetic>') => {
  const violations = []
  const lines = text.split('\n')
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    const code = line.replace(/\/\/.*/g, '').trim()
    if (!code) continue

    for (const { gate, pattern, skipIf } of FORBIDDEN) {
      if (skipIf && skipIf(file)) continue
      if (pattern.test(code)) {
        violations.push({ gate, file, line: i + 1, text: line.trim() })
      }
    }
  }
  return violations
}

/** Scan {file, text} entries. */
export const scanFiles = (entries) => {
  const violations = []
  for (const entry of entries) {
    const file = entry.file
    const text = entry.text
    for (const v of scanText(text, file)) violations.push(v)
    if (!isProcessPhysicalPath(file)) {
      // PR 9 item 4: multi-bool loop detection — two or more `let mutable x = false`
      // program-counter booleans together with a `while` loop is a state-machine
      // smell. Single resource-ownership flags (released/disposed) are not loops.
      const code = text.split('\n').map((l) => l.replace(/\/\/.*/g, ''))
      const bools = code.filter((l) => /\blet mutable\s+\w+\s*=\s*false\b/.test(l)).length
      const hasWhile = code.some((l) => /\bwhile\s/.test(l))
      if (bools >= 2 && hasWhile) {
        const first = code.findIndex((l) => /\blet mutable\s+\w+\s*=\s*false\b/.test(l))
        violations.push({
          gate: 'bool-loop',
          file,
          line: first + 1,
          text: `${bools} mutable false booleans with a while loop`,
        })
      }

      // PR 9 item 6: duplicate DU case-name sets within one file. Two DUs with
      // the exact same case names are the same knowledge in two shapes unless
      // the pair is a registered exemption (decision layer / boundary).
      const duRe = /^\s*(?:type|and)\s+(\w+)\s*=\s*/
      const caseRe = /^\s*\| ([A-Z]\w+)/
      const byCases = new Map()
      let cur = null
      const linesArr = text.split('\n')
      for (let i = 0; i < linesArr.length; i++) {
        const line = linesArr[i]
        const code2 = line.replace(/\/\/.*/g, '')
        const dm = duRe.exec(code2)
        if (dm) {
          cur = { name: dm[1], cases: [], line: i + 1 }
          for (const c of code2.matchAll(/\| ([A-Z]\w+)/g)) cur.cases.push(c[1])
          byCases.set(cur.name, cur)
          continue
        }
        if (cur) {
          const cm = caseRe.exec(code2)
          if (cm) {
            cur.cases.push(cm[1])
          } else if (code2.trim() && !/^\s*[{}]/.test(code2) && !code2.includes('of ') && code2.trim() !== '|') {
            cur = null
          }
        }
      }
      const base = norm(String(file)).split('/').pop() ?? ''
      for (const a of byCases.values()) {
        for (const b of byCases.values()) {
          if (a === b || a.cases.length < 2) continue
          const keyA = `${base}:${a.name}`
          const keyB = `${base}:${b.name}`
          if (DUP_CASES_EXEMPT.has(keyA) || DUP_CASES_EXEMPT.has(keyB)) continue
          if (a.cases.length === b.cases.length && a.cases.every((c, idx) => c === b.cases[idx])) {
            if (a.name < b.name) {
              violations.push({
                gate: 'dup-cases',
                file,
                line: a.line,
                text: `DU '${a.name}' repeats case set of '${b.name}': ${a.cases.join(' | ')}`,
              })
            }
          }
        }
      }
    }
  }
  return violations
}

export const groupByGate = (violations) => {
  const byGate = new Map()
  for (const v of violations) {
    if (!byGate.has(v.gate)) byGate.set(v.gate, [])
    byGate.get(v.gate).push(v)
  }
  return byGate
}

/** Exit decision: { ok, reason }. threshold < 0 means zero-tolerance. */
export const evaluateThreshold = (violationCount, threshold) => {
  if (violationCount === 0) return { ok: true, reason: 'clean' }
  if (threshold >= 0 && violationCount <= threshold) {
    return { ok: true, reason: 'within-threshold' }
  }
  if (threshold >= 0) return { ok: false, reason: 'exceeds-threshold' }
  return { ok: false, reason: 'fail-closed' }
}

const runCli = () => {
  const thresholdArg = process.argv.find((arg) => arg.startsWith('--threshold='))
  const threshold = thresholdArg ? Number(thresholdArg.split('=')[1]) : -1

  const productionFiles = walk(PRODUCTION_ROOT, ['.fs']).map(norm).filter(isProgramFile)
  const entries = productionFiles.map((file) => ({
    file,
    text: readFileSync(file, 'utf8'),
  }))
  const violations = scanFiles(entries)
  const byGate = groupByGate(violations)
  const write = threshold >= 0 ? console.log : console.error

  if (violations.length === 0) {
    write(`dsl-ownership: OK — ${productionFiles.length} Program/Domain files`)
    process.exit(0)
  }

  write(`dsl-ownership: ${violations.length} violation(s) — ${productionFiles.length} files\n`)
  for (const [gate, items] of byGate) {
    write(`${gate} (${items.length})`)
    for (const v of items) {
      write(`  ${v.file}:${v.line}  ${v.text}`)
    }
    write('')
  }

  const decision = evaluateThreshold(violations.length, threshold)
  if (decision.reason === 'within-threshold') {
    console.log(
      `dsl-ownership: within threshold — ${violations.length}/${threshold} violations (freeze only)`,
    )
    process.exit(0)
  }
  if (decision.reason === 'exceeds-threshold') {
    console.error(`dsl-ownership: exceeds threshold — ${violations.length} > ${threshold}`)
    process.exit(1)
  }
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
