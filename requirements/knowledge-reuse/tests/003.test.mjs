import assert from 'node:assert/strict'
import { mkdtempSync, realpathSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, relative } from 'node:path'
import test from 'node:test'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import { caseName, moved, runObserved } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

// Superseded note: direct Host/plugin tool names (`read`, `write`, `edit`, `glob`, `grep`, `mv`, `rm`)
// are retired; substantive access is exercised against `AccessTracker` and the programming surface's
// `cat`, `ls`, `stat`, `write`, `mv`, and `rm` members while preserving synthetic classification checks.

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-kr-003-'))
  return { dir: realpathSync(dir), cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

test('WHAT[knowledge-reuse-003] production access recorder collects committed reads, mutations and both move endpoints only', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'read.txt'), 'read-body\n', 'utf8')
    writeFileSync(join(dir, 'edited.txt'), 'before\n', 'utf8')
    writeFileSync(join(dir, 'gone.txt'), 'delete-me\n', 'utf8')
    writeFileSync(join(dir, 'old.txt'), 'move-me\n', 'utf8')
    writeFileSync(join(dir, 'mention.txt'), 'scan-only\n', 'utf8')

    const progTracker = casebook.createAccessTracker()
    const outcome = await runObserved(
      dir,
      'Engineer',
      'en',
      `class Js extends JsProgram {
  async run() {
    await this.ls('.');
    await this.stat('mention.txt');
    const r = await this.cat('read.txt');
    await this.cat('read.txt');
    const e = await this.cat('edited.txt');
    const [begin, end] = await this.where(e, 'before');
    this.write('edited.txt', e.slice(0, begin) + 'after' + e.slice(end));
    this.write('new.txt', 'created\\n');
    this.rm('gone.txt');
    this.mv('old.txt', 'moved.txt');
    return r.length;
  }
}`,
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
      (reads, effects) => {
        for (const read of reads) progTracker.recordRead(relative(dir, read.path), '')
        for (const effect of effects) progTracker.recordAttemptedMutation(effect, true)
      },
    )
    assert.equal(caseName(outcome), 'Succeeded')
    for (const pair of moved(outcome)) {
      const [source, destination] = pair.split(' -> ')
      progTracker.recordMove(source, destination)
    }
    assert.deepEqual(
      progTracker.getRelatedPaths().sort(),
      ['edited.txt', 'gone.txt', 'moved.txt', 'new.txt', 'old.txt', 'read.txt'],
    )
  } finally {
    cleanup()
  }

  const tracker = casebook.createAccessTracker()
  for (const [tool, args] of [
    ['read', { path: 'read.txt' }], ['create', { path: 'new.txt' }],
    ['edit', { path: 'edited.txt' }], ['rm', { path: 'gone.txt' }],
    ['mv', { source: 'old.txt', destination: 'moved.txt' }], ['read', { path: 'read.txt' }],
  ]) casebook.recordSubstantiveAccess(tracker, tool, args, true)
  for (const tool of ['grep', 'glob', 'ls', 'symbol', 'executor']) {
    casebook.recordSubstantiveAccess(tracker, tool, { path: 'mention.txt', command: 'cat mention.txt' }, true)
  }
  assert.deepEqual(tracker.getRelatedPaths().sort(), ['edited.txt', 'gone.txt', 'moved.txt', 'new.txt', 'old.txt', 'read.txt'])
})

test('WHAT[knowledge-reuse-003] rejected mutations add no paths and do not erase an earlier successful read', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'read.txt'), 'keep\n', 'utf8')
    writeFileSync(join(dir, 'from.txt'), 'unmoved\n', 'utf8')
    const progTracker = casebook.createAccessTracker()
    const readOutcome = await runObserved(
      dir,
      'Engineer',
      'en',
      `class Js extends JsProgram {
  async run() {
    return await this.cat('read.txt');
  }
}`,
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
      (reads, effects) => {
        for (const read of reads) progTracker.recordRead(relative(dir, read.path), '')
        for (const effect of effects) progTracker.recordAttemptedMutation(effect, true)
      },
    )
    assert.equal(caseName(readOutcome), 'Succeeded')

    let failedObserved = false
    const failedOutcome = await runObserved(
      dir,
      'Engineer',
      'en',
      `class Js extends JsProgram {
  async run() {
    this.write('failed.txt', 'uncommitted\\n');
    this.mv('from.txt', 'to.txt');
    throw new Error('rollback');
  }
}`,
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
      () => {
        failedObserved = true
      },
    )
    assert.equal(caseName(failedOutcome), 'Failed')
    assert.equal(failedObserved, false)
    assert.deepEqual(progTracker.getRelatedPaths(), ['read.txt'])
  } finally {
    cleanup()
  }

  const tracker = casebook.createAccessTracker()
  casebook.recordSubstantiveAccess(tracker, 'read', { path: 'read.txt' }, true)
  for (const tool of ['write', 'create', 'edit', 'rm', 'delete']) {
    casebook.recordSubstantiveAccess(tracker, tool, { path: 'failed.txt' }, false)
  }
  casebook.recordSubstantiveAccess(tracker, 'mv', { source: 'from.txt', destination: 'to.txt' }, false)
  assert.deepEqual(tracker.getRelatedPaths(), ['read.txt'])
})

test.todo('WHAT[knowledge-reuse-003] GAP-160: actual repository tools collect every successful whole-file dependency, including partial reads and failure after prior reads')
