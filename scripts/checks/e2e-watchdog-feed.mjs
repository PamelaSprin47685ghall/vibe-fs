#!/usr/bin/env node
// e2e watchdog-feed gate (ce.md §11.1).
// Top-level e2e tests must NOT call `watchdog.advance(`
// (or `watchdog?.advance(`) directly — only tests/e2e/support/* causal
// primitives may feed the watchdog. Any direct feed in scope is a violation.
//
// Modes:
//   node scripts/checks/e2e-watchdog-feed.mjs     exit 0 clean, exit 1 on violation
//
// Scope EXACTLY: top-level tests/e2e/*.test.mjs (One World sole entry).
// Do not require tests/e2e/cases/; missing or empty cases/ is fine.
// tests/e2e/support/* are the allowed feeders and are never flagged.

import { readdirSync, readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

// Repo root resolved from this script's location (scripts/checks -> root),
// so runCli works regardless of the caller's cwd.
export const ROOT = fileURLToPath(new URL('../..', import.meta.url))

// Only top-level e2e *.test.mjs files are forbidden by ce.md §11.1 under One World.
// Support files are the allowed causal feeders and must never be flagged.
export const WATCHDOG_FEED_PATTERN = /\bwatchdog\??\.\s*advance\s*\(/

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
 * Paths are resolved against ROOT.
 */
export const e2eTestCaseFiles = () => {
  const dir = join(ROOT, 'tests/e2e')
  let names
  try {
    names = readdirSync(dir)
  } catch {
    return []
  }
  return names
    .filter((name) => name.endsWith('.test.mjs'))
    .map((name) => norm(join(dir, name)))
    .sort()
}

const runCli = () => {
  const files = e2eTestCaseFiles()
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
