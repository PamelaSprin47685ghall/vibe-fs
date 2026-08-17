#!/usr/bin/env node
// e2e watchdog-feed gate (ce.md §11.1).
// Top-level e2e tests must NOT call `watchdog.advance(`
// (or `watchdog?.advance(`) directly — only tests/e2e/support/* causal
// primitives may feed the watchdog. Any direct feed in scope is a violation.
//
// Modes:
//   node scripts/checks/e2e-watchdog-feed.mjs     exit 0 clean, exit 1 on violation
//
// Scope EXACTLY: top-level e2e/*.test.mjs in the verification-system package
// (requirements/verification-system/tests/e2e/ — the One World sole entry,
// relocated from tests/e2e during the requirements cutover).
// Do not require e2e/cases/; missing or empty cases/ is fine.
// e2e/support/* are the allowed feeders and are never flagged.

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

// Repo root resolved from this script's location (scripts/checks -> root),
// so runCli works regardless of the caller's cwd.
export const ROOT = fileURLToPath(new URL('../..', import.meta.url))

// Only top-level e2e/*.test.mjs files are forbidden by ce.md §11.1 under One World.
// Support files are the allowed causal feeders and must never be flagged.
export const WATCHDOG_FEED_PATTERN = /\bwatchdog\??\.\s*advance\s*\(/

// One World: the verification-system package has exactly one real E2E entry.
// The e2e root must exist and contain this sole top-level entry; a missing or
// unreadable root, or a missing entry, is a fail-closed condition (never green
// with zero files). cases/ may be absent or empty (not required, not walked).
export const E2E_ROOT_REL = 'requirements/verification-system/tests/e2e'
export const SOLE_ENTRY = 'entry.test.mjs'

const norm = (p) => p.replace(/\\/g, '/')

/**
 * Scan each file's lines and return { file, line, text } for any line that
 * directly feeds the watchdog via `watchdog.advance(` / `watchdog?.advance(`.
 * The `\??` and `\s*` cover the optional `?` and any whitespace between the
 * member dot and the `advance(` call (e.g. a `{` opener on the same token).
 */
export const scanE2EWatchdogFeed = (files) => {
  const violations = []
  for (const file of files) {
    const lines = readFileSync(file, 'utf8').split('\n')
    for (let i = 0; i < lines.length; i++) {
      if (WATCHDOG_FEED_PATTERN.test(lines[i])) {
        violations.push({ file, line: i + 1, text: lines[i].trim() })
      }
    }
  }
  return violations
}

/**
 * Build the One World file list: top-level tests/e2e/*.test.mjs only.
 * Does not recurse into cases/ or support/. Missing or empty cases/ is ignored.
 *
 * Fail-closed invariants (ce.md §11.1 / One World sole entry):
 *   - The e2e root must exist, be readable, and be a directory — otherwise
 *     this throws (never returns [] to mask a missing/unreadable root).
 *   - The sole top-level entry {@link SOLE_ENTRY} must be present — otherwise
 *     this throws (never reports green with zero files).
 * Traversal/read errors are propagated, never swallowed.
 *
 * @param {string} [root=ROOT] Repo root to resolve the e2e root against.
 *   Injectable so tests can exercise fail-closed paths without mutating the repo.
 * @returns {string[]} Resolved, sorted top-level e2e *.test.mjs paths.
 */
export const e2eTestCaseFiles = (root = ROOT) => {
  const dir = resolve(root, E2E_ROOT_REL)

  let stat
  try {
    stat = statSync(dir)
  } catch (cause) {
    throw new Error(
      `e2e-watchdog-feed: e2e root missing or unreadable: ${norm(dir)}`,
      { cause },
    )
  }
  if (!stat.isDirectory()) {
    throw new Error(`e2e-watchdog-feed: e2e root is not a directory: ${norm(dir)}`)
  }

  let names
  try {
    names = readdirSync(dir)
  } catch (cause) {
    throw new Error(
      `e2e-watchdog-feed: e2e root unreadable: ${norm(dir)}`,
      { cause },
    )
  }

  const files = names
    .filter((name) => name.endsWith('.test.mjs'))
    .map((name) => norm(join(dir, name)))
    .sort()

  const entryPath = norm(join(dir, SOLE_ENTRY))
  if (!files.includes(entryPath)) {
    throw new Error(
      `e2e-watchdog-feed: missing sole top-level e2e entry ${SOLE_ENTRY} in ${norm(dir)}`,
    )
  }

  return files
}

const runCli = () => {
  let files
  try {
    files = e2eTestCaseFiles()
  } catch (err) {
    console.error(err.message)
    process.exit(1)
  }

  const violations = scanE2EWatchdogFeed(files)

  if (violations.length === 0) {
    console.log(`e2e-watchdog-feed: OK — ${files.length} top-level e2e test file(s)`)
    process.exit(0)
  }

  console.error(`e2e-watchdog-feed: ${violations.length} violation(s) — ${files.length} files\n`)
  for (const v of violations) {
    console.error(`  {{${v.file.replace(ROOT.replace(/\\/g, '/'), '').replace(/^\//, '')}:${v.line}}  ${v.text}`)
  }
  process.exit(1)
}

const isMain =
  process.argv[1] !== undefined &&
  resolve(fileURLToPath(import.meta.url)) === resolve(process.argv[1])

if (isMain) runCli()
