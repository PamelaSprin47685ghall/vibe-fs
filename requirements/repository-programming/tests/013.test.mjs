import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync, existsSync } from 'node:fs'
import { randomUUID } from 'node:crypto'
import { parse as parseToml } from 'smol-toml'
import { generate } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { create as createEventStore, dispose as disposeEventStore } from '../../../dist/Persistence/EventStore/Surface.js'
import { pending } from '../../../dist/Repository/Programming/Js/TransactionSurface.js'

// tests/unit/js-tools/js-tools-fs.test.mjs — G5 Phase B-4: filesystem adapter
// (JS-005/006/007/013/015).
//
// Strict UTF-8 reads, ordered anchor matching, full glob, all-or-nothing
// commit with rollback. Pure Node fs against per-test temp directories.


  readUtf8,
  glob,
  findAnchor,
  requireUnique,
  grep,
  commitPlan,
  rollbackPlan,
} from '../../../dist/Repository/Programming/Js/FilesystemSurface.js'

const exact = (text) => ({ kind: 'exact', text })
const regex = (text) => ({ kind: 'regex', text })

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-jstools-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const ok = (result) => result.ok
const codeOf = (result) => result.code
const unwrap = (result) => {
  assert.equal(result.ok, true, `expected Ok, got ${JSON.stringify(result.error)}`)
  return result.value
}

// JS-012/015: transaction decisions consume plain mutation facts; durable
// effects stay behind the EventStore-backed owner surfaces.


  validateSingleIntent,
  validateTargets,
  validateFreshness,
  preflight,
  commitPlan,
  rollbackPlan,
} from '../../../dist/Repository/Programming/Js/TransactionSurface.js'

const ok = (result) => result.ok
const codeOf = (result) => result.code
const rewrite = (path, originalText, newText) => ({ kind: 'rewrite', path, originalText, newText })
const create = (path, text) => ({ kind: 'create', path, text })

const current = { 'a.txt': 'current' }

// JS-085: sandbox → staging → preflight → commit is one owner-managed
// workflow. Result validation precedes commit and success is coupled to commit.


  run,
  runObserved,
  caseName,
  rewritten,
  created,
  failureCode,
  render,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-workflow-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const localStore = () => {
  const owned = mkdtempSync(join(tmpdir(), 'wxs-workflow-events-'))
  const commonDir = join(owned, '.git')
  mkdirSync(commonDir, { recursive: true })
  const handle = createEventStore(commonDir, randomUUID().replaceAll('-', ''))
  return { handle, close: () => { disposeEventStore(handle); rmSync(owned, { recursive: true, force: true }) } }
}

const coderSurface = () => generate('Coder', ['Read', 'Write', 'Edit', 'Glob', 'Grep'], 'en')
const runWorkflow = async (dir, program, { deadlineMs = 2000, store = null } = {}) => ({
  outcome: await run(dir, 'Coder', 'en', program, deadlineMs, Date.now() + 60_000, 1 << 20, store),
  surface: coderSurface(),
})

test('WHAT[REPOSITORY-PROGRAMMING-013] JS013_commitPlan_all_or_nothing', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'oldA', 'utf8')
    // commit two files
    const plan = [
      { kind: 'rewrite', path: 'a.txt', expectedCurrent: 'oldA', newText: 'newA' },
      { kind: 'create', path: 'b.txt', newText: 'newB' },
    ]
    assert.equal(ok(commitPlan(dir, plan)), true)
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'newA')
    assert.equal(readFileSync(join(dir, 'b.txt'), 'utf8'), 'newB')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-013] JS013_commitPlan_aborts_before_write_when_snapshot_fails', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'oldA', 'utf8')
    // a directory target cannot be snapshotted → Phase 1 aborts BEFORE any write
    mkdirSync(join(dir, 'blocked'))
    const plan = [
      { kind: 'rewrite', path: 'a.txt', expectedCurrent: 'oldA', newText: 'newA' },
      { kind: 'rewrite', path: 'blocked', expectedCurrent: 'old', newText: 'nope' },
    ]
    assert.equal(codeOf(commitPlan(dir, plan)), 'FILE_CHANGED')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'oldA', 'no write happened at all')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-013] JS013_commitPlan_rolls_back_written_files_on_write_failure', () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'oldA', 'utf8')
    // second target's parent directory does not exist → write fails → the
    // already-written first file must be rolled back (all-or-nothing)
    const plan = [
      { kind: 'rewrite', path: 'a.txt', expectedCurrent: 'oldA', newText: 'newA' },
      { kind: 'create', path: 'x/y.txt', newText: 'nope' },
    ]
    assert.equal(codeOf(commitPlan(dir, plan)), 'TRANSACTION_COMMIT_FAILED')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'oldA', 'first write rolled back')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-013] JS013_preflight_orders_rules_and_short_circuits', () => {
  // duplicate intent wins over everything
  assert.equal(
    codeOf(preflight(['a.txt'], current, [], [rewrite('a.txt', 'current', 'x'), create('a.txt', 'y')])),
    'DUPLICATE_MUTATION_TARGET',
  )
  // missing target beats freshness
  assert.equal(codeOf(preflight(['a.txt'], current, [], [rewrite('missing.txt', 'anything', 'x')])), 'FILE_CHANGED')
  // all good
  assert.equal(ok(preflight(['a.txt'], current, [], [rewrite('a.txt', 'current', 'x'), create('b.txt', 'y')])), true)
})

test('WHAT[REPOSITORY-PROGRAMMING-013] JS013_commit_plan_is_exact', () => {
  const mutations = [create('b.txt', 'newB'), rewrite('a.txt', 'oldA', 'newA')]
  assert.deepEqual(commitPlan(mutations), [
    { kind: 'rewrite', path: 'a.txt', expectedCurrent: 'oldA', newText: 'newA' },
    { kind: 'create', path: 'b.txt', newText: 'newB' },
  ])
})

test('WHAT[REPOSITORY-PROGRAMMING-013] JS085_workflow_reads_and_commits_rewrite', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    const view = await this.file('a.txt', [['begin', 'end', 'hello']]);
    this.rewrite('a.txt', view.text('^', 'begin') + 'goodbye' + view.text('end', '$'));
    return { before: view.text() };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(rewritten(outcome), ['a.txt'])
    assert.deepEqual(created(outcome), [])
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'goodbye world', 'committed to disk')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-013] JS085_workflow_commits_create_and_reports', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const program = `class Js extends JsProgram {
  async run() {
    await this.write('new.txt', 'fresh');
    return { ok: true };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(rewritten(outcome), [])
    assert.deepEqual(created(outcome), ['new.txt'])
    assert.equal(readFileSync(join(dir, 'new.txt'), 'utf8'), 'fresh')
  } finally {
    cleanup()
  }
})
