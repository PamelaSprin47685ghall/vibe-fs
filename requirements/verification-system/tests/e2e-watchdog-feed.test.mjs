/**
 * VERIFY-004 permanent gate: top-level e2e tests must NOT call
 * `watchdog.advance(` / `watchdog?.advance(` directly. Only
 * tests/e2e/support/* causal primitives may feed the watchdog.
 *
 * One World scope: sole top-level entry (tests/e2e/*.test.mjs). Does not
 * require tests/e2e/cases/; missing or empty cases/ must not throw.
 */
import assert from 'node:assert/strict'
import { existsSync, mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  E2E_ROOT_REL,
  SOLE_ENTRY,
  e2eTestCaseFiles,
  scanE2EWatchdogFeed,
} from '../../../scripts/checks/e2e-watchdog-feed.mjs'

test('WHAT[VERIFICATION-SYSTEM-002] sole top-level e2e entry is entry.test.mjs', () => {
  // One World：第 4 层恰好一个真实 E2E 入口。顶层文件清单必须包含
  // tests/e2e/entry.test.mjs（唯一 Long Stroke）。
  const files = e2eTestCaseFiles()

  assert.ok(
    files.some((file) => file.endsWith('/tests/e2e/entry.test.mjs') || file.endsWith('tests/e2e/entry.test.mjs')),
    'expected top-level sole entry e2e/entry.test.mjs (verification-system package) in scope',
  )
})

test('WHAT[VERIFICATION-SYSTEM-003] e2e case ceiling is zero — no cases/ channel', () => {
  // E2E_CASE_CEILING = 0：case 天花板只降不升。机器面 = 顶层清单不递归
  // cases/ 或 support/；缺失或空 cases/ 必须被容忍（不存在 = 零 case），
  // 不许 walk 或 require 该目录。
  const files = e2eTestCaseFiles()

  for (const file of files) {
    assert.ok(existsSync(file), `e2e top-level test file missing: ${file}`)
  }
})

test('WHAT[VERIFICATION-SYSTEM-006] top-level e2e tests never feed watchdog directly', () => {
  // watchdog 只由 support/ 因果原语投喂；顶层测试直接调用 watchdog.advance( 即违规。
  const files = e2eTestCaseFiles()

  const violations = []
  for (const file of files) {
    violations.push(...scanE2EWatchdogFeed([file]))
  }

  assert.equal(
    violations.length,
    0,
    'e2e top-level tests must not call watchdog.advance directly (VERIFY-004); they must use support causal primitives only. Violations: ' +
      JSON.stringify(violations),
  )
})

// --- Fail-closed regression (VERIFY-009 / VERIFY-005 / VERIFY-002) -------------
// The gate must never report green with zero files when its scope is broken.
// A missing/unreadable/non-directory e2e root, or a missing sole entry, is a
// fail-closed condition: e2eTestCaseFiles throws (and the CLI exits nonzero).
// These use an injectable root + throwaway temp dirs — the real repo is never
// mutated. cases/ may be absent or empty (VERIFY-003) and must NOT throw.

/** Build a throwaway repo root whose e2e dir is laid out per `layout`. */
const makeTempRoot = (layout) => {
  const root = mkdtempSync(join(tmpdir(), 'e2e-wdf-fc-'))
  const e2e = join(root, E2E_ROOT_REL)
  if (layout.e2eDir !== false) mkdirSync(e2e, { recursive: true })
  for (const name of layout.files ?? []) {
    writeFileSync(join(e2e, name), '// throwaway\n')
  }
  if (layout.e2eIsFile) {
    rmSync(e2e, { recursive: true, force: true })
    writeFileSync(e2e, 'not a directory\n')
  }
  return root
}

const cleanup = (root) => rmSync(root, { recursive: true, force: true })

test('WHAT[VERIFICATION-SYSTEM-009] missing e2e root fails closed, not green with zero files', () => {
  // A gate whose path criterion points at a non-existent directory is a fake
  // gate (always-passing). Missing root must throw, never return [].
  const root = mkdtempSync(join(tmpdir(), 'e2e-wdf-fc-'))
  try {
    assert.throws(
      () => e2eTestCaseFiles(root),
      /e2e root missing or unreadable/,
      'missing e2e root must fail closed (throw), not return []',
    )
  } finally {
    cleanup(root)
  }
})

test('WHAT[VERIFICATION-SYSTEM-009] non-directory e2e root fails closed', () => {
  const root = makeTempRoot({ e2eIsFile: true })
  try {
    assert.throws(
      () => e2eTestCaseFiles(root),
      /e2e root is not a directory/,
      'a file where the e2e root directory should be must fail closed',
    )
  } finally {
    cleanup(root)
  }
})

test('WHAT[VERIFICATION-SYSTEM-002] missing sole entry.test.mjs fails closed', () => {
  // One World：第 4 层恰好一个真实 E2E 入口。e2e dir exists but the sole
  // entry is absent → must throw, not report green with the other files.
  const root = makeTempRoot({ files: ['other.test.mjs'] })
  try {
    assert.throws(
      () => e2eTestCaseFiles(root),
      new RegExp(`missing sole top-level e2e entry ${SOLE_ENTRY}`),
      'missing sole entry.test.mjs must fail closed (throw)',
    )
  } finally {
    cleanup(root)
  }
})

test('WHAT[VERIFICATION-SYSTEM-003] missing or empty cases/ is allowed (no throw)', () => {
  // cases/ is not required and not walked. A valid e2e root with the sole
  // entry and NO cases/ directory must return exactly the entry — proving the
  // fail-closed tightening did not regress the documented cases/ tolerance.
  const root = makeTempRoot({ files: [SOLE_ENTRY] })
  try {
    const files = e2eTestCaseFiles(root)
    assert.equal(files.length, 1, 'only the sole top-level entry is in scope')
    assert.ok(
      files[0].endsWith(`/tests/e2e/${SOLE_ENTRY}`),
      `expected sole entry path, got ${files[0]}`,
    )
  } finally {
    cleanup(root)
  }
})

test('WHAT[VERIFICATION-SYSTEM-005] traversal errors are not masked (cause preserved)', () => {
  // fail-closed 义务（VERIFY-005）：遇数据损坏/边界失配时安全失败，不崩溃吞上下文。
  // The original fail-open path swallowed the traversal error into a green [].
  // Fail-closed means the underlying errno is preserved as `cause` so the
  // failure is explainable, not a silent zero-file OK.
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
