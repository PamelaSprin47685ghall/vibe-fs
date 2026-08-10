#!/usr/bin/env node
/**
 * Unified-store Phase 1 RED architecture gate (changes/active/storage.md §35–§37).
 *
 * Scanners:
 *   1. feature-ref — refs/wanxiang/ outside Persist/Git ownership (only store may appear there)
 *   2. schema-version-in-store-context — durable event/store protocol versioning (§36)
 *   3. git-bypass — direct git process invocations outside Git/Persist ownership (§37)
 *
 * Modes:
 *   node scripts/checks/unified-store-gate.mjs           scan production tree
 *   import { scanFeatureRef, ... } from ...              pure synthetic / fixture tests
 *
 * Does NOT implement EventStore.
 */

import { existsSync, readFileSync } from 'node:fs'
import { relative, resolve } from 'node:path'
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

export const SCANNER_IDS = Object.freeze([
  'feature-ref',
  'schema-version-in-store-context',
  'git-bypass',
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
 * Run all three scanners on one file body.
 * @param {string} text
 * @param {string} [file]
 * @returns {Violation[]}
 */
export const scanText = (text, file = '<synthetic>') => [
  ...scanFeatureRef(text, file),
  ...scanSchemaVersionInStoreContext(text, file),
  ...scanGitBypass(text, file),
]

/** @param {{ file: string, text: string }[]} entries */
export const scanFiles = (entries) => {
  const violations = []
  for (const entry of entries) {
    for (const hit of scanText(entry.text, entry.file)) violations.push(hit)
  }
  return violations
}

export const collectProductionEntries = (root = PRODUCTION_ROOT) => {
  if (!existsSync(root)) {
    throw new Error(`unified-store-gate: required directory '${root}' does not exist`)
  }
  return walk(root, ['.fs']).map((file) => ({
    file: norm(relative('.', file) || file),
    text: readFileSync(file, 'utf8'),
  }))
}

const runCli = () => {
  let entries
  try {
    entries = collectProductionEntries()
  } catch (err) {
    console.error(String(err && err.message ? err.message : err))
    process.exit(1)
  }

  const violations = scanFiles(entries)
  if (violations.length === 0) {
    console.log(
      `unified-store-gate: OK — ${entries.length} files, scanners=${SCANNER_IDS.join(',')}`,
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
