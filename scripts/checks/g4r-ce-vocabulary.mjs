#!/usr/bin/env node
/**
 * g4r-ce-vocabulary.mjs — G4R-CE Semantic CE Vocabulary static ratchets
 * (changes/active/rabbit.md §24).
 *
 * Fail-closed detectors for obsolete controller absence (§24.1) and raw-time
 * (§24.2). Pure scan* helpers accept injectable trees; direct invocation always
 * exits non-zero on a violation. There is no warning phase.
 *
 * Usage: node scripts/checks/g4r-ce-vocabulary.mjs
 */

import { existsSync, readFileSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const ROOT = fileURLToPath(new URL('../..', import.meta.url))
export const PRODUCTION_ROOT_REL = 'src/Wanxiangshu'

/**
 * §24.1 + S6 HostReviewProgram — paths that MUST remain absent.
 * Relative to src/Wanxiangshu/.
 */
export const OBSOLETE_CONTROLLER_PATHS = Object.freeze([
  'Application/Reconciliation/TurnCompletionProgram.fs',
  'Infrastructure/OpenCode/Tools/FinalityController.fs',
  'Session/ReviewController.fs',
  'Application/Reconciliation/ManagerLifecycleGate.fs',
  'Application/Reconciliation/ReviewerGuardState.fs',
  'Infrastructure/OpenCode/Orchestration/HostReviewProgram.fs',
])

/**
 * Layers where raw wall-clock / timer primitives are forbidden (§24.2).
 * Infrastructure / Process physical adapters are outside this scan.
 */
export const RAW_TIME_SCAN_LAYERS = Object.freeze([
  'Execution',
  'Mission',
  'Interaction',
  'Context',
  'Participant',
  'Repository',
  'Strength',
  'Foundation',
  'Change',
  'Enforcer',
  'Composition',
])

/**
 * Forbidden raw-time tokens in Domain / Application / Session.
 * Includes rabbit.md §24.2 plus Date.now (JS interop twin of UtcNow).
 */
export const RAW_TIME_TOKENS = Object.freeze([
  'DateTimeOffset.UtcNow',
  'DateTime.Now',
  'DateTime.UtcNow',
  'Date.now',
  'setTimeout',
  'timerTask',
])

/**
 * Allowlist hook for physical adapters that temporarily live under scanned
 * layers (posix paths relative to src/Wanxiangshu/, or repo-relative).
 * Exact file match OR prefix (trailing `/`).
 *
 * Exact Session files below are physical runtime/codec/mailbox implementations,
 * not Application business workflows. They own Host timestamps or timer waits;
 * ChildRecovery/HandleController business decisions receive time as data instead.
 * Process/ + Infrastructure/ are already outside RAW_TIME_SCAN_LAYERS.
 */
export const RAW_TIME_ALLOWLIST = Object.freeze([
  // Physical OpenCode / Host / Surface / Stdio adapter subdirectories
  'Execution/Delegation/Fork/OpenCode/',
  'Execution/Delegation/SyncDelegate/Surface.fs',
  'Mission/Finality/OpenCode/',
  'Repository/Investigation/Semble/Stdio.fs',
  'Repository/Knowledge/Casebook/BookkeeperRuntime.fs',
  'Repository/Programming/Js/OpenCode/',
  'Change/Host/',
  'Enforcer/Guidance/TipSurface.fs',
])

const norm = (p) => p.replace(/\\/g, '/')

/**
 * @param {string} file
 * @param {readonly string[]} [allowlist]
 */
export const isRawTimeAllowlisted = (file, allowlist = RAW_TIME_ALLOWLIST) => {
  const rel = norm(file)
  const candidates = [
    rel,
    rel.startsWith(`${PRODUCTION_ROOT_REL}/`)
      ? rel.slice(PRODUCTION_ROOT_REL.length + 1)
      : rel,
  ]
  for (const entry of allowlist) {
    const a = norm(entry)
    for (const c of candidates) {
      if (c === a) return true
      if (a.endsWith('/') && c.startsWith(a)) return true
      if (!a.endsWith('/') && c.startsWith(`${a}/`)) return true
    }
  }
  return false
}

/**
 * Presence of an obsolete controller path is a violation (absence ratchet).
 * Injectable `exists` for synthetic trees.
 *
 * @param {string[]} paths relative to production root
 * @param {(rel: string) => boolean} exists
 * @returns {{ path: string, kind: 'obsolete-controller', message: string }[]}
 */
export const scanObsoleteControllerAbsence = (paths, exists) => {
  const violations = []
  for (const path of paths) {
    const rel = norm(path)
    if (exists(rel)) {
      violations.push({
        kind: 'obsolete-controller',
        path: rel,
        message: `obsolete controller still present (G4R-CE Exit must delete): ${rel}`,
      })
    }
  }
  return violations
}

/**
 * Scan text entries for raw-time tokens. Skips allowlisted paths.
 *
 * @param {{ file: string, text: string }[]} entries
 * @param {{ allowlist?: readonly string[], tokens?: readonly string[] }} [opts]
 * @returns {{ kind: 'raw-time', file: string, line: number, token: string, text: string, message: string }[]}
 */
export const scanRawTimeEntries = (entries, opts = {}) => {
  const allowlist = opts.allowlist ?? RAW_TIME_ALLOWLIST
  const tokens = opts.tokens ?? RAW_TIME_TOKENS
  const violations = []
  for (const { file, text } of entries) {
    const fileNorm = norm(file)
    if (isRawTimeAllowlisted(fileNorm, allowlist)) continue
    const lines = text.split('\n')
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i]
      // Skip pure documentation / comments naming the forbidden tokens.
      const code = line.replace(/\/\/.*/, '')
      for (const token of tokens) {
        if (code.includes(token)) {
          violations.push({
            kind: 'raw-time',
            file: fileNorm,
            line: i + 1,
            token,
            text: line.trim(),
            message: `${fileNorm}:${i + 1} raw time '${token}' — ${line.trim().slice(0, 120)}`,
          })
        }
      }
    }
  }
  return violations
}

/**
 * Collect Domain/Application/Session source entries under a production root.
 * @param {string} productionRoot absolute path to src/Wanxiangshu
 * @param {readonly string[]} [layers]
 */
export const collectRawTimeScanEntries = (
  productionRoot,
  layers = RAW_TIME_SCAN_LAYERS,
) => {
  const entries = []
  for (const layer of layers) {
    const dir = join(productionRoot, layer)
    if (!existsSync(dir)) continue
    for (const abs of walk(dir, ['.fs', '.mjs', '.js', '.ts'])) {
      entries.push({
        file: norm(relative(productionRoot, abs) || abs),
        text: readFileSync(abs, 'utf8'),
      })
    }
  }
  return entries
}

/**
 * Full production scan (injectable root for fixtures).
 * @param {string} [repoRoot]
 * @param {{ allowlist?: readonly string[] }} [opts]
 */
export const scanG4RCeVocabulary = (repoRoot = ROOT, opts = {}) => {
  const productionRoot = join(repoRoot, PRODUCTION_ROOT_REL)
  const obsolete = scanObsoleteControllerAbsence(OBSOLETE_CONTROLLER_PATHS, (rel) =>
    existsSync(join(productionRoot, rel)),
  )
  const rawTime = scanRawTimeEntries(collectRawTimeScanEntries(productionRoot), {
    allowlist: opts.allowlist ?? RAW_TIME_ALLOWLIST,
  })
  return {
    obsolete,
    rawTime,
    violations: [...obsolete, ...rawTime],
  }
}

const runCli = () => {
  if (process.argv.slice(2).some((arg) => arg.startsWith('--phase='))) {
    console.error('g4r-ce-vocabulary: --phase is obsolete; direct invocation is always hard')
    process.exit(2)
  }

  const { obsolete, rawTime, violations } = scanG4RCeVocabulary(ROOT)
  const header = `g4r-ce-vocabulary: obsolete=${obsolete.length} raw-time=${rawTime.length}`

  if (violations.length === 0) {
    console.log(`${header} — clean`)
    process.exit(0)
  }

  console.error(`${header} — ${violations.length} hit(s)\n`)
  for (const v of violations) console.error(`  [${v.kind}] ${v.message}`)
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
