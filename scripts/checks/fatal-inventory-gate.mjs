#!/usr/bin/env node
/**
 * fatal-inventory-gate.mjs — execution-failure-policy-010 entry gate.
 *
 * Compares the scanned production fatal-entry set against
 * requirements/execution-failure-policy/fatal-inventory.json (the entry index,
 * not a second product policy). Fails when:
 *   (a) a scan finds a FatalProcess.trip / FatalProcess.kill / Diagnostic.fatal
 *       call site (or one-hop alias use) with no inventory row;
 *   (b) an inventory row's source symbol no longer matches the tree
 *       (operation string gone from that file) — proof is stale;
 *   (c) a row marked FixedWithRegression names a test file that does not exist.
 *
 * Scanning:
 *   - Direct calls: `FatalProcess.trip`, `FatalProcess.kill`, `Diagnostic.fatal`
 *     on masked F# code (comments/strings blanked via maskFSharpTrivia, so
 *     comment mentions and string literals never match). Definition files
 *     (Foundation/FatalProcess.fs, OpenCode/Host/Diagnostic.fs) are matched
 *     too — their rows are F32.
 *   - One-hop aliases: a point-free binding `Name = FatalProcess.trip` (F11
 *     shape) registers Name as a fatal alias; later uses of that alias as a
 *     member access (`deps.TripFatal ...`, F14 shape) or bare call are scanned
 *     as fatal call sites. `member _.ReportFatalDiagnostic` implementations
 *     that forward to FatalProcess.trip (F05/F06/F08 shape) register
 *     ReportFatalDiagnostic as an alias the same way, so a future
 *     `.ReportFatalDiagnostic(...)` consumer invocation is scanned too.
 *   - Allowlisted non-entries (proposal Appendix C): managed-child kills whose
 *     target is provably not self, signal-0 liveness probes, TextDecoder
 *     `fatal:true` decode options, `SendOutcome.Fatal` union traffic, and the
 *     git-hook runner's own exit codes. These live in resources/ or match a
 *     narrow shape documented below — they never reach this scanner's
 *     FatalProcess/Diagnostic patterns in the first place.
 *
 * Matching inventory rows to scanned sites is by stable operation string, not
 * line numbers (per proposal §3.3): each row carries one or more `operations`
 * (exact fatal operation strings) or the marker "dynamic" for caller-supplied
 * operation forwarders (F05/F06/F08/F13/F26), which match by source file
 * instead. See the gate fixture cases at the bottom of this file's test.
 */

import { existsSync, readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import { maskFSharpTrivia } from '../lib/fsharp-source.mjs'
import { walk } from '../lib/walk.mjs'

const here = join(fileURLToPath(new URL('.', import.meta.url)))
const repositoryRoot = resolve(here, '..', '..')
const normalize = (path) => path.replaceAll('\\', '/')

export const INVENTORY_REL = 'requirements/execution-failure-policy/fatal-inventory.json'
export const PRODUCTION_ROOT_REL = 'src/Wanxiangshu'

const DIRECT_FATAL_RE = /\b(?:FatalProcess\s*\.\s*(?:trip|kill)|Diagnostic\s*\.\s*fatal)\b/g
const POINT_FREE_ALIAS_RE = /\b([A-Z][A-Za-z0-9_']*)\s*=\s*FatalProcess\s*\.\s*trip\b/g
const FORWARDING_MEMBER_RE = /member\s+[_.A-Za-z0-9']*\.?\s*(ReportFatalDiagnostic)\s*\([^)]*\)\s*=/g

const repoRelative = (root, absolute) => normalize(absolute.slice(root.length + 1))

export const sourceFiles = (root) => {
  const sourceRoot = join(root, PRODUCTION_ROOT_REL)
  if (!existsSync(sourceRoot)) return []
  return walk(sourceRoot, ['.fs'])
    .map((path) => ({ path: repoRelative(root, path), text: readFileSync(path, 'utf8') }))
}

/**
 * Scan production F# sources for fatal-triggering call sites, resolving
 * one-hop aliases. Returns { sites, aliases } where each site is
 * { path, line, operation, via } and aliases maps alias name -> defining file.
 */
export function scanFatalSites(files) {
  const masked = files.map(({ path, text }) => ({
    path,
    text,
    code: maskFSharpTrivia(text),
    rawLines: text.split('\n'),
  }))

  // Pass 1: alias definitions shared across the whole tree.
  const aliases = new Map()
  const aliasDefLines = new Map()
  for (const { path, code, text } of masked) {
    const lines = code.split('\n')
    for (let index = 0; index < lines.length; index += 1) {
      const line = lines[index]
      POINT_FREE_ALIAS_RE.lastIndex = 0
      let hit
      while ((hit = POINT_FREE_ALIAS_RE.exec(line)) !== null) {
        if (!aliases.has(hit[1])) {
          aliases.set(hit[1], path)
          aliasDefLines.set(hit[1], index + 1)
        }
      }
      FORWARDING_MEMBER_RE.lastIndex = 0
      let member
      while ((member = FORWARDING_MEMBER_RE.exec(line)) !== null) {
        // Only a forwarder when the body reaches FatalProcess.trip: check a
        // small window of masked lines after the member head.
        const window = lines.slice(index, index + 6).join('\n')
        if (/FatalProcess\s*\.\s*trip/.test(window) && !aliases.has(member[1])) {
          aliases.set(member[1], path)
          aliasDefLines.set(member[1], index + 1)
        }
      }
    }
    void text
  }

  // Pass 2: call sites — direct fatal calls plus alias invocations.
  const sites = []
  for (const { path, code, rawLines } of masked) {
    const lines = code.split('\n')
    for (let index = 0; index < lines.length; index += 1) {
      const line = lines[index]
      DIRECT_FATAL_RE.lastIndex = 0
      let hit
      while ((hit = DIRECT_FATAL_RE.exec(line)) !== null) {
        // A point-free alias definition (`TripFatal = FatalProcess.trip`) is
        // the F11 binding row, not a call site. Trailing closers (record
        // braces, parens) may follow the primitive name.
        if (/=\s*FatalProcess\s*\.\s*(?:trip|kill)\s*[}\]\)]*\s*$/.test(line)) continue
        sites.push({ path, line: index + 1, operation: operationNear(rawLines, index), via: null })
      }
      for (const [name] of aliases) {
        const useRe = new RegExp(`(?:\\bdeps\\.${name}\\b|\\.${name}\\s*\\(|\\b${name}\\s+"|\\b${name}\\s*\\()`)
        if (useRe.test(line) && !(path === aliases.get(name) && index + 1 === aliasDefLines.get(name))) {
          // Skip the alias's own definition line and interface-only
          // declarations (`abstract ReportFatalDiagnostic ...` has no body).
          if (/^\s*abstract\s/.test(line)) continue
          if (new RegExp(`\\b${name}\\s*=\\s*FatalProcess`).test(line)) continue
          sites.push({ path, line: index + 1, operation: operationNear(rawLines, index), via: name })
        }
      }
    }
  }
  return { sites, aliases }
}

/** Best-effort operation string: nearest quoted literal on the call line or
 *  the following lines (F# argument lists often wrap onto the next lines).
 *  Only looks forward from the call line: a literal BEFORE the call belongs
 *  to an earlier statement (e.g. the fallback line above FatalProcess.kill
 *  in Diagnostic.fatal must not be read as the kill's operation). */
const operationNear = (lines, index) => {
  const callLine = lines[index] ?? ''
  // Direct primitives: literal must come AFTER the primitive name (a string
  // before it belongs to an earlier statement). Alias invocations
  // (`deps.TripFatal "op" detail`) carry the literal on the same line with
  // no primitive name present — take the first literal there.
  const parts = callLine.split(/FatalProcess\s*\.\s*(?:trip|kill)|Diagnostic\s*\.\s*fatal/)
  if (parts.length > 1) {
    const sameLine = /"([A-Za-z0-9][A-Za-z0-9_.:-]*)"/.exec(parts[1])
    if (sameLine) return sameLine[1]
  } else {
    const aliasLiteral = /"([A-Za-z0-9][A-Za-z0-9_.:-]*)"/.exec(callLine)
    if (aliasLiteral) return aliasLiteral[1]
  }
  const window = lines.slice(index + 1, index + 5).join('\n')
  const hit = /"([A-Za-z0-9][A-Za-z0-9_.:-]*)"/.exec(window)
  return hit ? hit[1] : null
}

export function loadInventory(root) {
  const absolute = join(root, INVENTORY_REL)
  if (!existsSync(absolute)) {
    return { ok: false, error: `fatal inventory missing: ${INVENTORY_REL}` }
  }
  let parsed
  try {
    parsed = JSON.parse(readFileSync(absolute, 'utf8'))
  } catch (err) {
    return { ok: false, error: `fatal inventory is not valid JSON: ${err.message}` }
  }
  if (!Array.isArray(parsed.entries)) {
    return { ok: false, error: 'fatal inventory has no entries array' }
  }
  return { ok: true, entries: parsed.entries }
}

const inventoryFileOf = (entry) => {
  const symbol = entry.sourceSymbol ?? ''
  const hit = /^(.*?)\s*::/.exec(symbol)
  if (!hit) return null
  const head = hit[1].trim()
  // Most rows live under src/Wanxiangshu/. Appendix C rows may name a
  // repo-relative path instead (resources/..., or a path called out in
  // prose). Resolve in order: literal repo-relative, then src/Wanxiangshu.
  const candidates = head.includes('/')
    ? [head, head.replace(/^.*?((?:resources|scripts|requirements)\/.*)$/, '$1')]
    : []
  const literal = candidates.find((candidate) => candidate !== head && candidate.includes('/'))
  if (literal) return literal
  const prose = /repo-relative\s+((?:resources|scripts|requirements)\/[^\s)]+)/.exec(symbol)
  if (prose) return prose[1]
  // Non-src production-adjacent paths (resources/...) are repo-relative as
  // written. Anything else is a src/Wanxiangshu/ source.
  if (/^(resources|scripts|requirements)\//.test(head)) return head
  return `src/Wanxiangshu/${head}`
}

const inventoryOperations = (entry) => {
  const operation = entry.operation ?? ''
  if (/dynamic/i.test(operation)) return null
  const literals = [...operation.matchAll(/"([A-Za-z0-9][A-Za-z0-9_.:-]*)"/g)].map((m) => m[1])
  if (literals.length > 0) return literals
  // Bare operation with prose suffix ("op (fixed string)"): the FIRST token
  // is the operation; the rest is human annotation, not matchable text.
  const head = /^[A-Za-z0-9][A-Za-z0-9_.:-]*/.exec(operation.trim())
  return head ? [head[0]] : []
}

/**
 * Gate logic over injected inputs (pure; used by check() and the gate's own
 * fixture tests). `files` are { path, text }; `testsExist` maps repo-relative
 * test path -> boolean.
 */
export function scanFatalInventory(files, entries, testsExist = () => true, root = repositoryRoot) {
  const violations = []
  const { sites } = scanFatalSites(files)
  const byFile = new Map()
  for (const { path, text } of files) byFile.set(path, maskFSharpTrivia(text))

  const covered = new Array(sites.length).fill(false)
  const rowUsed = new Array(entries.length).fill(false)
  const rawByFile = new Map(files.map(({ path, text }) => [path, text]))

  entries.forEach((entry, entryIndex) => {
    const file = inventoryFileOf(entry)
    const operations = inventoryOperations(entry)
    if (operations === null) {
      // Dynamic-operation forwarder: matches any scanned site in its file.
      sites.forEach((site, siteIndex) => {
        if (site.path === file) {
          covered[siteIndex] = true
          rowUsed[entryIndex] = true
        }
      })
      // Migration-tracking rows (C01/C02 shape): the fatal subject is
      // intentionally gone while the replacement lands. The row stays green
      // by file presence; the stale check below exempts it the same way.
      if (!sites.some((site) => site.path === file)) rowUsed[entryIndex] = true
      return
    }
    if (operations.length === 0) {
      // No matchable operation literal (alias binding F11, dead-handler C-rows,
      // excluded X-rows): verified by the stale-symbol check below instead.
      rowUsed[entryIndex] = true
      return
    }
    // One operation string may legitimately name several rows: distinct call
    // sites sharing the string (F19/F20/F21, F23/F24, F28-adjacent F18/F29,
    // F33/F34, F09/F10), or one call site covered by both a branch row and
    // the F12 funnel row (F15/T01-T11). Every row whose file+operation match
    // a site marks that site covered; the (b) stale check below keeps each
    // row honest independently.
    sites.forEach((site, siteIndex) => {
      if (site.operation && operations.includes(site.operation) && site.path === file) {
        covered[siteIndex] = true
        rowUsed[entryIndex] = true
      }
    })
  })

  // (a) scanned site with no inventory row.
  sites.forEach((site, siteIndex) => {
    if (!covered[siteIndex]) {
      const via = site.via ? ` via alias ${site.via}` : ''
      violations.push({
        code: 'fatal-entry-unregistered',
        path: site.path,
        detail: `line ${site.line}: fatal call${via} has no fatal-inventory.json row (operation: ${site.operation ?? 'dynamic'})`,
      })
    }
  })

  // (b) inventory row whose source symbol moved away (proof stale).
  entries.forEach((entry, entryIndex) => {
    if (entry.status === 'ExcludedWithEvidence') return
    const file = inventoryFileOf(entry)
    // Non-src files (resources/... git-hook runner) are not in the F# scan
    // set: existence on disk is their staleness signal, and they never
    // produce (a) violations (no FatalProcess/Diagnostic call to scan).
    if (file && !file.startsWith('src/')) {
      if (!existsSync(join(root, file))) {
        violations.push({
          code: 'fatal-inventory-stale-symbol',
          path: INVENTORY_REL,
          detail: `${entry.id}: source file ${file} no longer exists — proof is stale`,
        })
      }
      return
    }
    if (!file || !byFile.has(file)) {
      violations.push({
        code: 'fatal-inventory-stale-symbol',
        path: INVENTORY_REL,
        detail: `${entry.id}: source file ${file ?? entry.sourceSymbol} no longer exists — proof is stale`,
      })
      return
    }
    const operations = inventoryOperations(entry)
    if (operations === null) {
      // Dynamic forwarder: the file must still mention a fatal primitive or
      // the alias name, else the row's subject is gone.
      const code = byFile.get(file)
      const name = /ReportFatalDiagnostic|diagnosticFatal|emitFatalRecord|TripFatal/.exec(entry.sourceSymbol)
      // Migration-tracking rows declare the subject gone on purpose (the
      // replacement capability is landing in a sibling slice); file presence
      // is their only staleness signal. FixedWithRegression rows anchor on
      // their named regression test instead (check (c) verifies the file
      // exists): this covers migrations whose subject is intentionally no
      // longer a fatal call — e.g. a dead optional fuse replaced by a typed
      // return, guarded by a `has_no_optional_fatal_handler_path` test.
      const migrationTracking = /UNDER MIGRATION|migration-tracking/i.test(
        `${entry.triggerBranch ?? ''} ${entry.commitDisposition ?? ''}`,
      )
      const anchoredRegression =
        entry.status === 'FixedWithRegression' &&
        typeof entry.formalTestId === 'string' &&
        entry.formalTestId.split('::')[0].endsWith('.test.mjs') &&
        testsExist(entry.formalTestId.split('::')[0])
      if (!migrationTracking && !anchoredRegression && !DIRECT_FATAL_RE.test(code) && !(name && code.includes(name[0]))) {
        DIRECT_FATAL_RE.lastIndex = 0
        violations.push({
          code: 'fatal-inventory-stale-symbol',
          path: INVENTORY_REL,
          detail: `${entry.id}: ${file} no longer contains its fatal subject — proof is stale`,
        })
      }
      DIRECT_FATAL_RE.lastIndex = 0
      return
    }
    if (operations.length === 0) {
      void rowUsed[entryIndex]
      return
    }
    const code = byFile.get(file)
    // Presence is checked on RAW text: maskFSharpTrivia blanks string
    // literals by design, so masked code never contains the quotes.
    const raw = rawByFile.get(file) ?? ''
    const missing = operations.filter((operation) => !raw.includes(`"${operation}"`))
    if (missing.length > 0) {
      violations.push({
        code: 'fatal-inventory-stale-symbol',
        path: INVENTORY_REL,
        detail: `${entry.id}: operation ${missing.map((m) => `"${m}"`).join(', ')} no longer in ${file} — proof is stale`,
      })
    }
  })

  // (c) FixedWithRegression naming a test that does not exist.
  for (const entry of entries) {
    if (entry.status !== 'FixedWithRegression' || !entry.formalTestId) continue
    const testPath = entry.formalTestId.split('::')[0]
    if (!testPath.endsWith('.test.mjs') || !testsExist(testPath)) {
      violations.push({
        code: 'fatal-inventory-missing-regression-test',
        path: INVENTORY_REL,
        detail: `${entry.id}: FixedWithRegression names a test that does not exist: ${entry.formalTestId}`,
      })
    }
  }

  return violations
}

export function check(context) {
  const root = context?.root ?? repositoryRoot
  let files
  if (context?.productionFiles) {
    files = context.productionFiles()
      .filter(({ file }) => file.endsWith('.fs'))
      .map(({ file, text }) => ({ path: file, text }))
  } else {
    files = sourceFiles(root)
  }
  const loaded = loadInventory(root)
  if (!loaded.ok) {
    return { issues: [{ code: 'fatal-inventory-missing', path: INVENTORY_REL, message: loaded.error }] }
  }
  const testsExist = (rel) =>
    context?.readText ? (() => { try { context.readText(rel); return true } catch { return false } })() : existsSync(join(root, rel))
  const violations = scanFatalInventory(files, loaded.entries, testsExist, root)
  return {
    issues: violations.map((v) => ({ code: v.code, path: v.path, message: v.detail ?? v.code })),
  }
}

export function runCli() {
  const result = check()
  if (result.issues.length > 0) {
    console.error('fatal-inventory-gate FAILED:')
    for (const issue of result.issues) console.error(`  - ${issue.path}: [${issue.code}] ${issue.message}`)
    return 1
  }
  const loaded = loadInventory(repositoryRoot)
  console.log(`fatal-inventory-gate: OK — ${loaded.ok ? loaded.entries.length : 0} inventory rows cover every scanned fatal entry`)
  return 0
}

const isMain = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)
if (isMain) {
  const code = runCli()
  if (code !== 0) process.exit(code)
}
