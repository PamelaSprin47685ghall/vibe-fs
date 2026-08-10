#!/usr/bin/env node
/**
 * g4r-ce-vocabulary.mjs — G4R-CE Semantic CE Vocabulary static ratchets
 * (changes/active/rabbit.md §24).
 *
 * S0 RED scaffolding (landing order §27):
 *   - Detectors for obsolete controller absence (§24.1) and raw-time (§24.2).
 *   - Unit tests prove scanners fail closed on synthetic trees.
 *   - Production still retains obsolete controllers / Session raw time —
 *     therefore `npm run check` must NOT hard-fail on the live tree yet.
 *
 * Phases:
 *   --phase=s0-soft   (default) scan production; print hits; exit 0 (warn-mode)
 *   --phase=hard      fail-closed on any hit (S14 / Exit; do not use in check.mjs yet)
 *
 * Pure scan* helpers accept injectable trees so tests never depend on deleting
 * production controllers.
 *
 * G4R-CE-S0: not wired into scripts/check.mjs hard fail. Wire as s0-soft or
 * leave commented until S14. See scripts/check.mjs note.
 *
 * Usage:
 *   node scripts/checks/g4r-ce-vocabulary.mjs
 *   node scripts/checks/g4r-ce-vocabulary.mjs --phase=s0-soft
 *   node scripts/checks/g4r-ce-vocabulary.mjs --phase=hard
 */

import { existsSync, readFileSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const ROOT = fileURLToPath(new URL('../..', import.meta.url))
export const PRODUCTION_ROOT_REL = 'src/Wanxiangshu'

/**
 * §24.1 + S6 HostReviewProgram — paths that MUST be absent at G4R-CE Exit.
 * Relative to src/Wanxiangshu/. Presence today is expected; S0 only scaffolds
 * the detector. Do NOT delete these in S0.
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
export const RAW_TIME_SCAN_LAYERS = Object.freeze(['Domain', 'Application', 'Session'])

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
  // G4R-5: Session CompletedAt/CreatedAt/timers sink to IClockPort/ITimerPort.
  // Keep empty; re-add only with explicit physical-adapter justification.
])

export const PHASES = Object.freeze(['s0-soft', 'hard'])

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

/**
 * @param {string[]} argv
 * @returns {'s0-soft' | 'hard'}
 */
export const parsePhase = (argv = process.argv.slice(2)) => {
  for (const arg of argv) {
    if (arg.startsWith('--phase=')) {
      const phase = arg.slice('--phase='.length)
      if (phase === 's0-soft' || phase === 'hard') return phase
      throw new Error(
        `g4r-ce-vocabulary: unknown --phase=${phase} (expected s0-soft|hard)`,
      )
    }
  }
  return 's0-soft'
}

const runCli = () => {
  let phase
  try {
    phase = parsePhase()
  } catch (err) {
    console.error(String(err?.message ?? err))
    process.exit(2)
  }

  const { obsolete, rawTime, violations } = scanG4RCeVocabulary(ROOT)

  const header =
    `g4r-ce-vocabulary [${phase}]: obsolete=${obsolete.length} raw-time=${rawTime.length} ` +
    `(G4R-CE §24 S0 RED scaffolding)`

  if (violations.length === 0) {
    console.log(`${header} — clean`)
    process.exit(0)
  }

  const stream = phase === 'hard' ? console.error : console.warn
  stream(`${header} — ${violations.length} hit(s)\n`)
  for (const v of violations) {
    stream(`  [${v.kind}] ${v.message}`)
  }

  if (phase === 's0-soft') {
    stream(
      `\nG4R-CE-S0: warn-only. Production may still retain obsolete controllers / raw time. ` +
        `Unit tests prove detectors on synthetic trees. Harden at S14 (--phase=hard).`,
    )
    process.exit(0)
  }

  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
