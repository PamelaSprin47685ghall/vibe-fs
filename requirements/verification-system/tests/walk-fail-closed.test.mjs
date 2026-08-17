// requirements/verification-system/tests/walk-fail-closed.test.mjs
//
// VERIFICATION-SYSTEM-005 (fail-closed) machine落点 for scripts/lib/walk.mjs.
//
// walk is the shared directory walker every layer-0 static gate and test runner
// uses to enumerate source trees. Its old shape swallowed three classes of
// failure as an empty array — missing root, nested readdir error, and symlink
// entries — so a gate that scanned nothing reported OK forever. That is the
// exact pseudo-gate shape VERIFY-004/009 forbids: a check whose criterion
// cannot reach the filesystem it claims to describe.
//
// This test proves the three fail-open paths are now observable failures:
//   1. missing root throws (not [])
//   2. nested unreadable directory throws (not silent skip)
//   3. symlink entry is rejected (not followed, not silently skipped)
//   4. non-directory root throws (not [root])
//   5. a normal tree still returns sorted matching paths (regression guard)
//
// Fixtures are real temporary directories/files/symlinks so the proof is
// behavioural, not a source-text assertion.

import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, symlinkSync, writeFileSync, chmodSync } from 'node:fs'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import test from 'node:test'

import { walk } from '../../../scripts/lib/walk.mjs'

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
    // Remove read+execute permission so readdirSync fails.
    // On root this test may still pass through; guard the assertion so a
    // permission change that did not take effect does not produce a false green.
    try {
      chmodSync(nested, 0o000)
    } catch {
      // Some filesystems reject chmod; skip the nested-permission assertion
      // only if the permission could not be applied at all.
      return
    }
    assert.throws(
      () => walk(dir, ['.fs']),
      /walk: readdir failed/,
      'a nested unreadable directory must throw so hidden content cannot evade a scan',
    )
  } finally {
    // Restore permissions before removal so rmSync can clean up.
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
  // Regression guard: the fail-closed hardening must not break the successful path.
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
