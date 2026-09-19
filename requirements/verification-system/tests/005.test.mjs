import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { existsSync, mkdtempSync, mkdirSync, writeFileSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const { E2E_ROOT_REL, SOLE_ENTRY, e2eTestCaseFiles, scanE2EWatchdogFeed } = await import("./e2e/support/watchdog-feed-scan.mjs");

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

test('WHAT[verification-system-005] traversal errors are not masked (cause preserved)', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { verify, verificationSteps } = await import("../../../scripts/verify.mjs");
const { checks } = await import("../../../scripts/check.mjs");
const { existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, readlinkSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { basename, dirname, join, resolve } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { default: test } = await import("node:test");

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
function createMemorySink() {
  let buf = ''
  return {
    write(chunk) {
      buf += chunk
    },
    get output() {
      return buf
    },
  }
  }
for (const failingLabel of ['format:check', 'check', 'build']) {
  test(`WHAT[verification-system-001] verify halts and marks subsequent steps not-run when ${failingLabel} fails`, async () => {
    const tmpLogDir = mkdtempSync(join(tmpdir(), 'proof-ladder-fail-'))
    const sink = createMemorySink()
  const spawned = []
  const fakeRunStep = async ({ label, argv }) => {
      spawned.push(label)
      if (label === failingLabel) {
        return { label, ok: false, exitCode: 1, signal: null, durationMs: 5 }
  }
      return { label, ok: true, exitCode: 0, signal: null, durationMs: 5 }
    }

    try {
      const result = await verify({
        release: false,
        runStep: fakeRunStep,
        output: sink,
        logDirectory: tmpLogDir,
})
      assert.equal(result.exitCode, 1)
      assert.equal(result.outcome, 'fail')
      assert.equal(spawned.at(-1), failingLabel, `execution must stop after ${failingLabel}`)

      const failedIdx = result.steps.findIndex((s) => s.label === failingLabel)
      assert.ok(failedIdx >= 0)
      assert.equal(result.steps[failedIdx].status, 'failed')
      for (let i = failedIdx + 1; i < result.steps.length; i++) {
        assert.equal(result.steps[i].status, 'not-run', `step ${result.steps[i].label} must be marked not-run`)
    }
      assert.match(sink.output, /FAIL  verify daily/)
    } finally {
      rmSync(tmpLogDir, { recursive: true, force: true })
    }
})
  }

test('WHAT[verification-system-005] check.mjs propagates nonzero fail-closed', () => {
  const checkSource = read('scripts/check.mjs')
  assert.match(
    checkSource,
    /process\.exit\(code\)/,
    'check.mjs must propagate nonzero exit code (a gate that fails must exit closed)',
  )
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { StrictMockSignals } = await import("./e2e/support/strict-mock-signals.js");


test('WHAT[verification-system-005] waitAny fatal cancellation removes every registered waiter', async () => {
  const signals = new StrictMockSignals();
  const waiting = signals.waitForAnyExpectation(['original.1', 'guarded.0']);

  signals.fail(new Error('provider mismatch'));

  await assert.rejects(waiting, /provider mismatch/);
  assert.equal(signals._expectationWaiters.size, 0);
});
test('WHAT[verification-system-005] waitAny rejects an open or malformed alternative set', async () => {
  const signals = new StrictMockSignals();

  await assert.rejects(signals.waitForAnyExpectation('original.1'), /array of at least two/);
  await assert.rejects(signals.waitForAnyExpectation(['original.1', 'original.1']), /unique non-blank/);
});
}

{
const { default: assert } = await import("node:assert/strict");
const { mkdirSync, mkdtempSync, rmSync, symlinkSync, writeFileSync, chmodSync } = await import("node:fs");
const { join } = await import("node:path");
const { tmpdir } = await import("node:os");
const { default: test } = await import("node:test");
const { walk } = await import("../../../scripts/lib/walk.mjs");


test('WHAT[verification-system-005] walk throws on a missing root instead of returning an empty array', () => {
  const missing = join(tmpdir(), 'walk-fail-closed-missing-' + process.pid)
  rmSync(missing, { recursive: true, force: true })
  assert.throws(
    () => walk(missing, ['.fs']),
    /walk: root .* is not accessible/,
    'a missing root must throw so a gate cannot scan nothing and report OK',
  )
})
test('WHAT[verification-system-005] walk throws on a non-directory root instead of returning [root]', () => {
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
test('WHAT[verification-system-005] walk throws on a nested unreadable directory instead of silently skipping it', () => {
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
test('WHAT[verification-system-005] walk rejects a symlink entry instead of following or skipping it', () => {
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
test('WHAT[verification-system-005] walk rejects a symlink root instead of following it', () => {
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
test('WHAT[verification-system-005] walk returns sorted matching paths on a normal tree', () => {
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
test('WHAT[verification-system-005] walk preserves the SKIP directory set', () => {
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
}
