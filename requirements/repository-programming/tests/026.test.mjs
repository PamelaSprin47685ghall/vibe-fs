import assert from 'node:assert/strict'
import { mkdtempSync, realpathSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { createAccessTracker } from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import { runObserved, caseName } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

// Superseded note: direct Host tool names (`read`, `edit`, `glob`, `grep`) in
// `recordSubstantiveAccess` are retired; substantive access is now driven by the js programming
// surface (`cat`, `ls`, `stat`, `write`, `mv`, `rm`) and recorded on `AccessTracker`.

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-substantive-026-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

test('WHAT[repository-programming-026] case capture ignores discovery and records explicit reads', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'scan-only.fs'), 'module ScanOnly\n', 'utf8')
    writeFileSync(join(dir, 'read.fs'), 'module ReadTarget\n', 'utf8')
    const observedReads = []
    const observedEffects = []
    const outcome = await runObserved(
      dir,
      'Engineer',
      'en',
      `class Js extends JsProgram {
  async run() {
    const entries = await this.ls('.');
    const info = await this.stat('scan-only.fs');
    const text = await this.cat('read.fs');
    await this.cat('read.fs');
    return { count: entries.length, exists: info.exists, len: text.length };
  }
}`,
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
      (reads, effects) => {
        observedReads.push(...reads)
        observedEffects.push(...effects)
      },
    )
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(observedEffects, [])
    assert.deepEqual(
      observedReads.map((r) => r.path),
      [realpathSync(join(dir, 'read.fs'))],
    )

    const tracker = createAccessTracker()
    tracker.recordGlob('*')
    tracker.recordGrep('ScanOnly', 'scan-only.fs')
    tracker.recordRead('read.fs', '')
    tracker.recordRead('read.fs', '')
    assert.deepEqual(tracker.getRelatedPaths(), ['read.fs'])
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-026] case capture records mutations only after their committed outcome', async () => {
  for (const committed of [false, true]) {
    const tracker = createAccessTracker()
    tracker.recordAttemptedMutation('changed.fs', committed)
    assert.deepEqual(tracker.getRelatedPaths(), committed ? ['changed.fs'] : [])
  }
  const moveTracker = createAccessTracker()
  moveTracker.recordMove('before.fs', 'after.fs')
  assert.deepEqual(moveTracker.getRelatedPaths().sort(), ['after.fs', 'before.fs'])

  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'implicit-only.fs'), 'original\n', 'utf8')
    let failedObservationCalled = false
    const failedOutcome = await runObserved(
      dir,
      'Engineer',
      'en',
      `class Js extends JsProgram {
  async run() {
    this.write('implicit-only.fs', 'updated\\n');
    throw new Error('abort after write');
  }
}`,
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
      () => {
        failedObservationCalled = true
      },
    )
    assert.equal(caseName(failedOutcome), 'Failed')
    assert.equal(failedObservationCalled, false, 'failed program must not report committed substantive access')

    const committedReads = []
    const committedEffects = []
    const okOutcome = await runObserved(
      dir,
      'Engineer',
      'en',
      `class Js extends JsProgram {
  async run() {
    this.write('implicit-only.fs', 'rewrite without cat\\n');
    return { ok: true };
  }
}`,
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
      (reads, effects) => {
        committedReads.push(...reads)
        committedEffects.push(...effects)
      },
    )
    assert.equal(caseName(okOutcome), 'Succeeded')
    assert.deepEqual(committedReads, [], 'internal write snapshot must not leak into explicit reads')
    assert.deepEqual(committedEffects, ['implicit-only.fs'])
  } finally {
    cleanup()
  }
})

test.todo('WHAT[repository-programming-026] actual program reads and commit outcomes feed case access without copying conflict snapshots')
