#!/usr/bin/env node
/**
 * Unified-store architecture gate (changes/active/storage.md §35–§37 + Amendment G3.5-A / P4U2).
 *
 * Scanners (Phase 1–3):
 *   1. feature-ref — refs/wanxiang/ outside Persist/Git ownership (only store may appear there)
 *   2. schema-version-in-store-context — durable event/store protocol versioning (§36)
 *   3. git-bypass — direct git process invocations outside Git/Persist ownership (§37)
 *
 * Scanners (P4U2 GATE-NO-MIGRATOR / clean-break):
 *   4. student-qa-revival — StudentQaStore / QA.md store paths under src/ (cross-check:
 *      scripts/checks/student-teacher-absence.mjs already ratchets StudentQa* tokens; this
 *      scanner keeps the unified-store gate fail-closed on storage-path revival)
 *   5. no-migrator — one-shot legacy importer / LegacyProjection≡NewProjection tooling
 *   6. dual-write — same production module writing EventStore AND Journal NDJSON
 *
 * Dual-write note (Phase 5 DONE for NDJSON substrate): production Boot / NDJSON
 * JournalWriter / dir BlobWriter are deleted. Strategy A keeps AgentJournal as an
 * EventStore-backed surface — AgentJournal-only modules are NOT dual-write.
 * Persist/EventStore-only modules are NOT dual-write. This scanner only flags
 * detectable same-module bridges that write both surfaces. DUAL_WRITE_ALLOWLIST
 * stays empty unless a documented false-positive must be named; do not use it to
 * park real bridges.
 *
 * Modes:
 *   node scripts/checks/unified-store-gate.mjs           scan production + clean-break roots
 *   import { scanFeatureRef, ... } from ...              pure synthetic / fixture tests
 *
 * Does NOT implement EventStore.
 */

import { existsSync, readFileSync } from 'node:fs'
import { basename, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

export const PRODUCTION_ROOT = 'src/Wanxiangshu'

/** Paths (posix, relative to PRODUCTION_ROOT) allowed to own the canonical store ref / Git primitives. */
export const STORE_OWNER_PREFIXES = ['Infrastructure/Persist/', 'Infrastructure/Git/']

/**
 * Extra production sites allowed to mention git process tokens outside Persist/Git.
 * Wave C cleared this: host callers go through Infrastructure/Git/GitSubject.fs.
 * New bypasses must NOT be added here — route through Infrastructure/Git (eventually GitGateway).
 */
export const GIT_BYPASS_ALLOWLIST = Object.freeze([])

/**
 * Host / authored-resource schemaVersion sites that are NOT durable event-store protocol
 * versioning (SPEC unknowns + §36: product/host/authored versions are allowed).
 * Listed for documentation; the store-context heuristic already excludes them.
 */
export const NON_STORE_SCHEMA_VERSION_SITES = Object.freeze([
  'Domain/EnforcerCatalog.fs',
  'Infrastructure/Resources/EnforcerCatalogResource.fs',
  'Session/HandleCompletionCodec.fs',
  'Domain/ChildRecovery.fs',
])

/**
 * Documented dual-write false-positive allowlist (posix paths relative to repo root).
 * Keep empty: Strategy A AgentJournal-only code is not a hit; do not allowlist real
 * EventStore+Journal bridges. Phase 5 deleted NDJSON writers — only same-module dual writers RED.
 */
export const DUAL_WRITE_ALLOWLIST = Object.freeze([])

/**
 * Paths (repo-relative posix) / prefixes skipped by no-migrator tree scan so the gate and its
 * intentional RED fixtures may name forbidden patterns without self-failing.
 */
export const NO_MIGRATOR_PATH_ALLOWLIST = Object.freeze([
  'scripts/checks/unified-store-gate.mjs',
  'tests/unit/verify/unified-store-gate.test.mjs',
  'tests/unit/verify/fixtures/',
])

export const SCANNER_IDS = Object.freeze([
  'feature-ref',
  'schema-version-in-store-context',
  'git-bypass',
  'student-qa-revival',
  'no-migrator',
  'dual-write',
])

const norm = (p) => p.replace(/\\/g, '/')

const stripLineComment = (line) => line.replace(/\/\/.*/, '')

/** @param {string} file */
export const relToProduction = (file) => {
  const n = norm(file)
  const root = `${PRODUCTION_ROOT}/`
  if (n.startsWith(root)) return n.slice(root.length)
  if (n.startsWith('src/Wanxiangshu/')) return n.slice('src/Wanxiangshu/'.length)
  return n
}

/** @param {string} rel */
export const isStoreOwnerPath = (rel) =>
  STORE_OWNER_PREFIXES.some((prefix) => rel.startsWith(prefix) || rel === prefix.slice(0, -1))

/** @param {string} rel */
export const isGitBypassAllowed = (rel) =>
  isStoreOwnerPath(rel) || GIT_BYPASS_ALLOWLIST.includes(rel)

/** @param {string} file */
export const isNoMigratorPathAllowed = (file) => {
  const n = norm(file)
  return NO_MIGRATOR_PATH_ALLOWLIST.some((entry) => {
    if (entry.endsWith('/')) return n.startsWith(entry)
    return n === entry || n.endsWith(`/${entry}`)
  })
}

/** @param {string} file */
export const isDualWriteAllowed = (file) => {
  const n = norm(file)
  return DUAL_WRITE_ALLOWLIST.some((entry) => n === entry || n.endsWith(`/${entry}`))
}

/**
 * @typedef {{ id: string, file: string, line: number, label: string, text: string }} Violation
 */

const FEATURE_REF_RE = /refs\/wanxiang\//
const CANONICAL_STORE_REF = 'refs/wanxiang/store'

/** Always-forbidden durable store protocol version tokens (§36). */
const ALWAYS_STORE_VERSION_RES = [
  { re: /\bstorageVersion\b/, token: 'storageVersion' },
  { re: /\bjournalVersion\b/, token: 'journalVersion' },
  { re: /\bformatVersion\b/, token: 'formatVersion' },
  { re: /\bschema_version\b/, token: 'schema_version' },
  { re: /\bStoreV2\b/, token: 'StoreV2' },
  { re: /\bJournalV2\b/, token: 'JournalV2' },
  { re: /\/events\/v2\//, token: '/events/v2/' },
  { re: /refs\/wanxiang\/store-v2/, token: 'refs/wanxiang/store-v2' },
]

/** schemaVersion only RED when store/event envelope context is present nearby. */
const SCHEMA_VERSION_RE = /\bschemaVersion\b/
const STORE_CONTEXT_RE =
  /\b(?:EventEnvelope|EventStore|IEventStore|StoreSnapshot|AppendCandidate|GitRawStore|payload_refs|event_type)\b|refs\/wanxiang\/store\b|durable\s+event\s+store|event\s+store\s+protocol/i

/** Line-local git process / argv construction (F# + Fable). */
const GIT_BYPASS_LINE_RES = [
  /\bFileName\s*=\s*"git"/,
  /\bexecFileSync\s*\(\s*"git"/,
  /\bexecFile\s*\(\s*"git"/,
  /\bProcess\.Start\s*\(\s*"git"/,
  /\bspawn(?:Sync)?\s*\(\s*"git"/,
  /\bcommand\s+\w+\s+"git"/,
  /^\s*"git"\s*$/,
]

/** Multiline forms (execFileSync "git" across lines). */
const GIT_BYPASS_MULTILINE_RES = [
  /\bexecFileSync\b[\s\S]{0,80}"git"/,
  /\bexecFile\b[\s\S]{0,80}"git"/,
]

/** Owner-only remote-tracking store tip: refs/wanxiang/remotes/<remote>/store (§14). */
const REMOTE_TRACKING_STORE_REF_RE = /^refs\/wanxiang\/remotes\/[^/]+\/store$/

/**
 * Student QA storage revival under src/ (Amendment G3.5-A / G3 clean-break).
 * Complementary to student-teacher-absence.mjs (do not weaken that gate).
 */
const STUDENT_QA_REVIVAL_RES = [
  { re: /\bStudentQaStore\b/, token: 'StudentQaStore' },
  { re: /(?:^|[\s"'`([{,/\\])QA\.md\b/, token: 'QA.md' },
  { re: /\bStudentQa(?:Opened|Closed|Question|Answer)\b/, token: 'StudentQa* event/API' },
]

/**
 * Legacy migrator / importer tooling (P4U2 GATE-NO-MIGRATOR).
 * Intentionally narrow: live Journal observers that only read wanxiangshu-next are OK.
 */
const LEGACY_PROJECTION_EQUIV_RE =
  /\bLegacyProjection\b\s*(?:[=≡]=|===|==|≡)\s*\bNewProjection\b|\bNewProjection\b\s*(?:[=≡]=|===|==|≡)\s*\bLegacyProjection\b/

const NO_MIGRATOR_TOKEN_RES = [
  { re: /\bLegacyMigrator\b/, token: 'LegacyMigrator' },
  { re: /\bLegacyImporter\b/, token: 'LegacyImporter' },
  { re: /\blegacyImporter\b/, token: 'legacyImporter' },
  { re: /\bmigrateLegacy(?:Journal|StudentQa|Qa)?\b/, token: 'migrateLegacy*' },
  { re: /\bStudentQaMigrator\b/, token: 'StudentQaMigrator' },
  { re: /\bJournalToEventStore\b/, token: 'JournalToEventStore' },
  { re: /\blegacy\s+importer\b/i, token: 'legacy importer' },
  { re: /\bone-shot\s+migrat(?:or|ion)\b/i, token: 'one-shot migrator/migration' },
]

const LEGACY_NDJSON_RE = /wanxiangshu-next|\.ndjson\b/
const EVENTSTORE_MIGRATE_SINK_RE =
  /\b(?:IEventStore|EventStore|AppendCandidate|LegacyProjection|NewProjection)\b/

/** Same-module EventStore write + Journal NDJSON write (dual-write bridge). */
const EVENT_STORE_WRITE_RE =
  /\b(?:IEventStore|AppendCandidate)\b|\bEventStore\.(?:append|create|createWithConverge|createWithRetries|commit)\b/
const JOURNAL_NDJSON_WRITE_RE =
  /\b(?:JournalWriter|AgentJournal|SharedAgentJournal)\b|\.ndjson\b|wanxiangshu-next/

/**
 * feature-ref: any refs/wanxiang/ outside Persist/Git; non-canonical store refs always RED.
 * Persist/Git may also own remote-tracking store tips (`refs/wanxiang/remotes/<remote>/store`).
 * @param {string} text
 * @param {string} [file]
 * @returns {Violation[]}
 */
export const scanFeatureRef = (text, file = '<synthetic>') => {
  const rel = relToProduction(file)
  const owner = file === '<synthetic>' ? false : isStoreOwnerPath(rel)
  const lines = text.split('\n')
  const hits = []

  for (let i = 0; i < lines.length; i++) {
    const raw = lines[i]
    const code = stripLineComment(raw)
    if (!FEATURE_REF_RE.test(code)) continue

    // Include `%` so F# sprintf format strings like remotes/%s/store extract fully.
    const refs = code.match(/refs\/wanxiang\/[A-Za-z0-9._/%-]*/g) || []
    for (const ref of refs) {
      if (ref === CANONICAL_STORE_REF || ref.startsWith(`${CANONICAL_STORE_REF}/`)) {
        if (owner) continue
        hits.push({
          id: 'feature-ref',
          file,
          line: i + 1,
          label: `canonical store ref '${CANONICAL_STORE_REF}' only allowed under Infrastructure/Persist|Git`,
          text: raw.trim(),
        })
      } else if (owner && REMOTE_TRACKING_STORE_REF_RE.test(ref)) {
        continue
      } else {
        hits.push({
          id: 'feature-ref',
          file,
          line: i + 1,
          label: `feature-owned ref '${ref}' forbidden; only '${CANONICAL_STORE_REF}' may exist`,
          text: raw.trim(),
        })
      }
    }
  }
  return hits
}

/**
 * schema-version-in-store-context: §36 durable event/store protocol versioning.
 * @param {string} text
 * @param {string} [file]
 * @returns {Violation[]}
 */
export const scanSchemaVersionInStoreContext = (text, file = '<synthetic>') => {
  const lines = text.split('\n')
  const hits = []
  const codeLines = lines.map(stripLineComment)

  for (let i = 0; i < codeLines.length; i++) {
    const code = codeLines[i]
    const raw = lines[i]

    for (const { re, token } of ALWAYS_STORE_VERSION_RES) {
      if (re.test(code)) {
        hits.push({
          id: 'schema-version-in-store-context',
          file,
          line: i + 1,
          label: `durable store protocol version token '${token}' is forbidden (§36)`,
          text: raw.trim(),
        })
      }
    }

    if (SCHEMA_VERSION_RE.test(code)) {
      const lo = Math.max(0, i - 8)
      const hi = Math.min(codeLines.length, i + 9)
      const window = codeLines.slice(lo, hi).join('\n')
      if (STORE_CONTEXT_RE.test(window) || STORE_CONTEXT_RE.test(code)) {
        hits.push({
          id: 'schema-version-in-store-context',
          file,
          line: i + 1,
          label:
            "schemaVersion in event/store context is forbidden; additive vocabulary only (§36)",
          text: raw.trim(),
        })
      }
    }
  }
  return hits
}

/**
 * git-bypass: direct git invocation outside Infrastructure/Git|Persist (plus documented allowlist).
 * @param {string} text
 * @param {string} [file]
 * @returns {Violation[]}
 */
export const scanGitBypass = (text, file = '<synthetic>') => {
  const rel = relToProduction(file)
  if (file !== '<synthetic>' && isGitBypassAllowed(rel)) return []

  const lines = text.split('\n')
  const codeLines = lines.map(stripLineComment)
  const hits = []
  const seenLines = new Set()

  const pushHit = (lineIdx, raw) => {
    if (seenLines.has(lineIdx)) return
    seenLines.add(lineIdx)
    hits.push({
      id: 'git-bypass',
      file,
      line: lineIdx + 1,
      label:
        'direct git process invocation outside Infrastructure/Git|Persist (must use GitGateway ownership)',
      text: raw.trim(),
    })
  }

  for (let i = 0; i < codeLines.length; i++) {
    for (const re of GIT_BYPASS_LINE_RES) {
      if (re.test(codeLines[i])) {
        pushHit(i, lines[i])
        break
      }
    }
  }

  const joined = codeLines.join('\n')
  for (const re of GIT_BYPASS_MULTILINE_RES) {
    const m = joined.match(re)
    if (!m || typeof m.index !== 'number') continue
    const lineIdx = joined.slice(0, m.index).split('\n').length - 1
    pushHit(lineIdx, lines[lineIdx] || m[0])
  }

  return hits
}

/**
 * student-qa-revival: fail closed if StudentQaStore / QA.md store paths return under src/.
 * @param {string} text
 * @param {string} [file]
 * @returns {Violation[]}
 */
export const scanStudentQaRevival = (text, file = '<synthetic>') => {
  if (file !== '<synthetic>') {
    const n = norm(file)
    // Scope: production src/ tree (and synthetic/fixture paths used in unit tests).
    // tests/scripts/docs mentions are out of scope here — student-teacher-absence covers
    // StudentQa* tokens under src/ (+ prompts); this scanner owns storage-path revival.
    if (n.startsWith('tests/') || n.startsWith('scripts/') || n.startsWith('archive/')) return []
  }

  const lines = text.split('\n')
  const hits = []
  for (let i = 0; i < lines.length; i++) {
    const raw = lines[i]
    for (const { re, token } of STUDENT_QA_REVIVAL_RES) {
      if (re.test(raw)) {
        hits.push({
          id: 'student-qa-revival',
          file,
          line: i + 1,
          label: `Student QA storage revival token '${token}' is forbidden under src/ (G3 / G3.5-A clean-break; see also student-teacher-absence.mjs)`,
          text: raw.trim(),
        })
        break
      }
    }
  }
  return hits
}

/**
 * no-migrator: fail closed on one-shot legacy→EventStore migrators and
 * LegacyProjection≡NewProjection suites (Amendment G3.5-A / P4U2 GATE-NO-MIGRATOR).
 * @param {string} text
 * @param {string} [file]
 * @returns {Violation[]}
 */
export const scanNoMigrator = (text, file = '<synthetic>') => {
  if (file !== '<synthetic>' && isNoMigratorPathAllowed(file)) return []

  const lines = text.split('\n')
  const hits = []
  const seen = new Set()

  const push = (lineIdx, label, raw) => {
    const key = `${lineIdx}:${label}`
    if (seen.has(key)) return
    seen.add(key)
    hits.push({
      id: 'no-migrator',
      file,
      line: lineIdx + 1,
      label,
      text: raw.trim(),
    })
  }

  for (let i = 0; i < lines.length; i++) {
    const raw = lines[i]
    const code = stripLineComment(raw)

    if (LEGACY_PROJECTION_EQUIV_RE.test(code) || LEGACY_PROJECTION_EQUIV_RE.test(raw)) {
      push(
        i,
        'LegacyProjection≡NewProjection (or ==) equivalence suite is forbidden for retired domains (G3.5-A)',
        raw,
      )
    }

    for (const { re, token } of NO_MIGRATOR_TOKEN_RES) {
      if (re.test(code) || re.test(raw)) {
        push(
          i,
          `legacy migrator/importer token '${token}' is forbidden (P4U2 GATE-NO-MIGRATOR; leave-unread clean-break only)`,
          raw,
        )
      }
    }
  }

  // Heuristic: persist migration test / migrator script that reads wanxiangshu-next NDJSON
  // into EventStore / projection-equivalence machinery.
  const base = basename(norm(file))
  const looksLikeMigratorFile =
    file !== '<synthetic>' &&
    (/^migration\.test\./i.test(base) ||
      /migrat(?:or|ion)/i.test(base) ||
      /\/persist\/migration/i.test(norm(file)))

  if (looksLikeMigratorFile) {
    const joined = lines.map(stripLineComment).join('\n')
    if (LEGACY_NDJSON_RE.test(joined) && EVENTSTORE_MIGRATE_SINK_RE.test(joined)) {
      const lineIdx = Math.max(
        0,
        lines.findIndex((l) => LEGACY_NDJSON_RE.test(l) || EVENTSTORE_MIGRATE_SINK_RE.test(l)),
      )
      push(
        lineIdx,
        'one-shot migrator tooling that reads wanxiangshu-next/.ndjson into EventStore (or projection equivalence) is forbidden (G3.5-A)',
        lines[lineIdx] || base,
      )
    }
  }

  return hits
}

/**
 * dual-write: same production module writes EventStore AND Journal surface tokens.
 * Strategy A AgentJournal-only (EventStore-backed) is OK; Persist/EventStore-only OK.
 * NDJSON JournalWriter/Boot production APIs are deleted (Phase 5).
 * @param {string} text
 * @param {string} [file]
 * @returns {Violation[]}
 */
export const scanDualWrite = (text, file = '<synthetic>') => {
  if (file !== '<synthetic>' && isDualWriteAllowed(file)) return []

  const code = text
    .split('\n')
    .map(stripLineComment)
    .join('\n')

  const hasEventStoreWrite = EVENT_STORE_WRITE_RE.test(code)
  const hasJournalWrite = JOURNAL_NDJSON_WRITE_RE.test(code)
  if (!(hasEventStoreWrite && hasJournalWrite)) return []

  const lines = text.split('\n')
  const codeLines = lines.map(stripLineComment)
  let lineIdx = 0
  for (let i = 0; i < codeLines.length; i++) {
    if (EVENT_STORE_WRITE_RE.test(codeLines[i]) || JOURNAL_NDJSON_WRITE_RE.test(codeLines[i])) {
      lineIdx = i
      break
    }
  }

  return [
    {
      id: 'dual-write',
      file,
      line: lineIdx + 1,
      label:
        'dual-write bridge: same module writes EventStore and Journal surface (forbidden; AgentJournal-only OK, EventStore-only OK)',
      text: (lines[lineIdx] || '').trim(),
    },
  ]
}

/**
 * Run Phase 1–3 + clean-break scanners on one file body.
 * @param {string} text
 * @param {string} [file]
 * @returns {Violation[]}
 */
export const scanText = (text, file = '<synthetic>') => [
  ...scanFeatureRef(text, file),
  ...scanSchemaVersionInStoreContext(text, file),
  ...scanGitBypass(text, file),
  ...scanStudentQaRevival(text, file),
  ...scanNoMigrator(text, file),
  ...scanDualWrite(text, file),
]

/** @param {{ file: string, text: string }[]} entries */
export const scanFiles = (entries) => {
  const violations = []
  for (const entry of entries) {
    for (const hit of scanText(entry.text, entry.file)) violations.push(hit)
  }
  return violations
}

/** Clean-break only (for extra tree roots beyond production .fs). */
export const scanCleanBreakText = (text, file = '<synthetic>') => [
  ...scanStudentQaRevival(text, file),
  ...scanNoMigrator(text, file),
  ...scanDualWrite(text, file),
]

export const collectProductionEntries = (root = PRODUCTION_ROOT) => {
  if (!existsSync(root)) {
    throw new Error(`unified-store-gate: required directory '${root}' does not exist`)
  }
  return walk(root, ['.fs']).map((file) => ({
    file: norm(relative('.', file) || file),
    text: readFileSync(file, 'utf8'),
  }))
}

/**
 * Extra entries for no-migrator fail-closed coverage (tests/scripts tooling).
 * Production .fs are already scanned via collectProductionEntries.
 */
export const collectNoMigratorExtraEntries = () => {
  const roots = [
    { dir: 'tests', exts: ['.mjs', '.js', '.fs'] },
    { dir: 'scripts', exts: ['.mjs', '.js'] },
  ]
  const entries = []
  for (const { dir, exts } of roots) {
    if (!existsSync(dir)) continue
    for (const file of walk(dir, exts)) {
      const rel = norm(relative('.', file) || file)
      if (isNoMigratorPathAllowed(rel)) continue
      entries.push({ file: rel, text: readFileSync(file, 'utf8') })
    }
  }
  return entries
}

const runCli = () => {
  let production
  try {
    production = collectProductionEntries()
  } catch (err) {
    console.error(String(err && err.message ? err.message : err))
    process.exit(1)
  }

  const violations = [
    ...scanFiles(production),
    ...collectNoMigratorExtraEntries().flatMap((entry) => scanNoMigrator(entry.text, entry.file)),
  ]

  if (violations.length === 0) {
    console.log(
      `unified-store-gate: OK — ${production.length} production files, scanners=${SCANNER_IDS.join(',')}`,
    )
    process.exit(0)
  }

  console.error(`unified-store-gate: ${violations.length} violation(s)\n`)
  for (const v of violations) {
    console.error(`  [${v.id}] ${v.file}:${v.line}  ${v.label}`)
    console.error(`    ${v.text.slice(0, 160)}`)
  }
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
