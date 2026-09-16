import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync, chmodSync } from 'node:fs'
import { join, dirname, resolve } from 'node:path'
import { tmpdir } from 'node:os'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import { walk } from '../../../scripts/lib/walk.mjs'
import { StrictMockSignals } from './e2e/support/strict-mock-signals.js'
import { e2eTestCaseFiles } from './e2e/support/watchdog-feed-scan.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
const cleanup = (root) => rmSync(root, { recursive: true, force: true })

// ── Shared directory walker fail-closed regressions ─────────────────────────

test('WHAT[VERIFICATION-SYSTEM-005] walk throws on a missing root instead of returning an empty array', () => {
  const missing = join(tmpdir(), 'walk-fail-closed-missing-' + process.pid)
  rmSync(missing, { recursive: true, force: true })
  assert.throws(
    () => walk(missing, ['.fs']),
    /walk: root .* is not accessible/,
    'a missing root must throw so a gate cannot scan nothing and report OK',
  )
})

test('WHAT[VERIFICATION-SYSTEM-005] walk throws on a non-directory root instead of returning [root]', () => {
  const dir = mkdtempSync(join(tmpdir(), 'walk-fail-closed-file-root-'))
  try {
    const file = join(dir, 'leaf.fs')
    writeFileSync(file, 'module X\n')
    assert.throws(
      () => walk(file, ['.fs']),
      /walk: root .* is not a directory/,
      'a non-directory root must throw so a gate cannot treat a single file as a scanned tree',
    )
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-005] walk throws on a nested unreadable directory instead of silently skipping it', () => {
  const dir = mkdtempSync(join(tmpdir(), 'walk-fail-closed-nested-'))
  try {
    const nested = join(dir, 'nested')
    mkdirSync(nested)
    writeFileSync(join(nested, 'hidden.fs'), 'module Hidden\n')
    try {
      chmodSync(nested, 0o000)
    } catch {
      return
    }
    assert.throws(
      () => walk(dir, ['.fs']),
      /walk: readdir failed/,
      'a nested unreadable directory must throw so hidden content cannot evade a scan',
    )
  } finally {
    try { chmodSync(join(dir, 'nested'), 0o755) } catch {}
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-005] walk rejects a symlink entry instead of following or skipping it', () => {
  const dir = mkdtempSync(join(tmpdir(), 'walk-fail-closed-symlink-'))
  try {
    const target = join(dir, 'target.fs')
    writeFileSync(target, 'module Target\n')
    const link = join(dir, 'link.fs')
    symlinkSync(target, link)
    assert.throws(
      () => walk(dir, ['.fs']),
      /walk: refusing to traverse symlink/,
      'a symlink entry must be rejected so hidden content cannot evade a scan via a link',
    )
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-005] walk rejects a symlink root instead of following it', () => {
  const dir = mkdtempSync(join(tmpdir(), 'walk-fail-closed-symlink-root-'))
  try {
    const realDir = join(dir, 'real')
    mkdirSync(realDir)
    writeFileSync(join(realDir, 'a.fs'), 'module A\n')
    const linkDir = join(dir, 'linkdir')
    symlinkSync(realDir, linkDir)
    assert.throws(
      () => walk(linkDir, ['.fs']),
      /walk: refusing to traverse symlink root/,
      'a symlink root must be rejected so a gate cannot follow a link into hidden content',
    )
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-005] walk returns sorted matching paths on a normal tree', () => {
  const dir = mkdtempSync(join(tmpdir(), 'walk-fail-closed-ok-'))
  try {
    mkdirSync(join(dir, 'sub'))
    writeFileSync(join(dir, 'b.fs'), 'module B\n')
    writeFileSync(join(dir, 'a.fs'), 'module A\n')
    writeFileSync(join(dir, 'sub', 'c.fs'), 'module C\n')
    writeFileSync(join(dir, 'ignore.txt'), 'noise\n')
    const result = walk(dir, ['.fs'])
    assert.deepEqual(
      result,
      [join(dir, 'a.fs'), join(dir, 'b.fs'), join(dir, 'sub', 'c.fs')].sort(),
      'a normal tree must return sorted paths matching the extension filter',
    )
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[VERIFICATION-SYSTEM-005] walk preserves the SKIP directory set', () => {
  const dir = mkdtempSync(join(tmpdir(), 'walk-fail-closed-skip-'))
  try {
    mkdirSync(join(dir, 'node_modules'))
    mkdirSync(join(dir, 'src'))
    writeFileSync(join(dir, 'node_modules', 'hidden.fs'), 'module Hidden\n')
    writeFileSync(join(dir, 'src', 'visible.fs'), 'module Visible\n')
    writeFileSync(join(dir, 'top.fs'), 'module Top\n')
    const result = walk(dir, ['.fs'])
    assert.deepEqual(
      result,
      [join(dir, 'src', 'visible.fs'), join(dir, 'top.fs')].sort(),
      'SKIP directories (node_modules, .git, etc.) must be preserved',
    )
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

// ── StrictMockSignals fail-closed ───────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-005] waitAny fatal cancellation removes every registered waiter', async () => {
  const signals = new StrictMockSignals()
  const waiting = signals.waitForAnyExpectation(['original.1', 'guarded.0'])

  signals.fail(new Error('provider mismatch'))

  await assert.rejects(waiting, /provider mismatch/)
  assert.equal(signals._expectationWaiters.size, 0)
})

test('WHAT[VERIFICATION-SYSTEM-005] waitAny rejects an open or malformed alternative set', async () => {
  const signals = new StrictMockSignals()

  await assert.rejects(signals.waitForAnyExpectation('original.1'), /array of at least two/)
  await assert.rejects(signals.waitForAnyExpectation(['original.1', 'original.1']), /unique non-blank/)
})

// ── check.mjs fail-closed propagation ────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-005] check.mjs propagates nonzero fail-closed', () => {
  const checkSource = read('scripts/check.mjs')
  assert.match(
    checkSource,
    /process\.exit\(code\)/,
    'check.mjs must propagate nonzero exit code (a gate that fails must exit closed)',
  )
})

// ── E2E watchdog feed traversal fail-closed ─────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-005] traversal errors are not masked (cause preserved)', () => {
  const root = mkdtempSync(join(tmpdir(), 'e2e-wdf-fc-'))
  try {
    let thrown
    try {
      e2eTestCaseFiles(root)
    } catch (err) {
      thrown = err
    }
    assert.ok(thrown, 'missing root must throw')
    assert.match(thrown.message, /^e2e-watchdog-feed:/, 'error carries the gate prefix')
    assert.ok(thrown.cause, 'underlying traversal error is preserved as cause (not swallowed)')
  } finally {
    cleanup(root)
  }
})
