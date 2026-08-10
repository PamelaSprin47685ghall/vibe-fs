// tests/unit/js-tools/js-workflow.test.mjs — G5 Phase B-4.5: the full js-*
// tool invocation workflow (sandbox → staging → preflight → commit).
//
// JS-085: result validation happens before commit; the commit is
// all-or-nothing; success return is coupled to commit (JS-067).

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync, existsSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { JsToolWorkflow_run as workflowRun } from '../../../dist/Infrastructure/OpenCode/Tools/JsToolWorkflow.js'
import { JsToolGenerator_generate as generate } from '../../../dist/Domain/JsTools.js'
import { ToolPermission } from '../../../dist/Kernel/Roles.js'
import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'
import { caseOf, listItems, resultOf } from '../support/domain.mjs'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-workflow-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const permissionComparer = { Compare: (a, b) => a.CompareTo(b) }
const coderCaps = ofArray(
  [ToolPermission.Read, ToolPermission.Write, ToolPermission.Edit, ToolPermission.Glob, ToolPermission.Grep],
  permissionComparer,
)

const runWorkflow = async (dir, program, { deadlineMs = 2000 } = {}) => {
  const surface = generate('Coder', coderCaps)
  const outcome = await workflowRun(
    dir,
    surface.BaseClassSource,
    program,
    deadlineMs,
    Date.now() + 60_000,
    1 << 20,
  )
  return { outcome, surface }
}

const caseName = (outcome) => caseOf(outcome)

test('JS085_workflow_reads_and_commits_rewrite', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    const view = await this.file('a.txt');
    await this.rewrite('a.txt', { find: 'hello', replace: 'goodbye' });
    return { before: view.text };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(JSON.parse(outcome.fields[0]), { before: 'hello world' })
    assert.deepEqual(listItems(outcome.fields[1]), ['a.txt']) // written
    assert.deepEqual(listItems(outcome.fields[2]), []) // created
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'goodbye world', 'committed to disk')
  } finally {
    cleanup()
  }
})

test('JS085_workflow_commits_create_and_reports', async () => {
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
    assert.deepEqual(listItems(outcome.fields[1]), []) // written
    assert.deepEqual(listItems(outcome.fields[2]), ['new.txt']) // created
    assert.equal(readFileSync(join(dir, 'new.txt'), 'utf8'), 'fresh')
  } finally {
    cleanup()
  }
})

test('JS085_workflow_preflight_blocks_stale_rewrite_without_touching_disk', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'current text', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    const r = await this.rewrite('a.txt', { find: 'stale', replace: 'x' });
    return { rewriteOk: r.ok, code: r.code };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    // the binding surfaces ANCHOR_NOT_FOUND; the program chose to continue,
    // so the workflow commits nothing and reports the program result
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(JSON.parse(outcome.fields[0]), { rewriteOk: false, code: 'ANCHOR_NOT_FOUND' })
    assert.deepEqual(listItems(outcome.fields[1]), [])
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'current text')
  } finally {
    cleanup()
  }
})

test('JS085_workflow_file_missing_surfaces_as_object_not_throw', async () => {
  const { dir, cleanup } = sandbox()
  try {
    // a missing target is a typed result the program can inspect (JS-019:
    // foreseeable failures are not exceptions)
    const program = `class Js extends JsProgram {
  async run() {
    const view = await this.file('missing.txt');
    return { found: view.ok, code: view.code };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(JSON.parse(outcome.fields[0]), { found: false, code: 'FILE_NOT_FOUND' })
  } finally {
    cleanup()
  }
})

test('JS085_workflow_program_error_fails_without_commit', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'old', 'utf8')
    const program = `class Js extends JsProgram {
  async run() { throw new Error('boom'); }
}`
    const { outcome } = await runWorkflow(dir, program)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(outcome.fields[0].tag, 1) // ProgramFailed
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'old', 'no commit on program failure')
  } finally {
    cleanup()
  }
})

test('JS012_workflow_with_store_persists_prepare_and_commit', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const raw = (await import('../../../dist/Infrastructure/Persist/GitRawStore.js')).GitRawStore_createInMemory()
    const store = (await import('../../../dist/Infrastructure/Persist/EventStore.js')).EventStore_create(raw)
    const surface = generate('Coder', coderCaps)
    const program = `class Js extends JsProgram {
  async run() {
    await this.rewrite('a.txt', { find: 'hello', replace: 'goodbye' });
    return { done: true };
  }
}`
    // F# option Some(tuple) → the tuple array itself
    const outcome = await workflowRun(dir, surface.BaseClassSource, program, 2000, Date.now() + 60_000, 1 << 20, [store, raw])
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'goodbye world', 'committed to disk')

    const storeModule = await import('../../../dist/Infrastructure/JsToolsTransactionStore.js')
    const events = resultOf(storeModule.loadEvents(raw, store.OpenSnapshot()))
    assert.equal(events.ok, true)
    assert.deepEqual(listItems(events.value).map((e) => e.EventType).sort(), ['JsTransactionCommitted', 'JsTransactionPrepared'])
    assert.deepEqual(listItems(storeModule.scanUncommitted(events.value)), [], 'no uncommitted after commit fact')
  } finally {
    cleanup()
  }
})

test('JS016_result_renders_stable_toml_shapes', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const surface = generate('Coder', coderCaps)
    const program = `class Js extends JsProgram {
  async run() {
    await this.rewrite('a.txt', { find: 'hello', replace: 'goodbye' });
    return { before: 'x' };
  }
}`
    const outcome = await workflowRun(dir, surface.BaseClassSource, program, 2000, Date.now() + 60_000, 1 << 20)
    const { JsToolsResult_render: render } = await import('../../../dist/Infrastructure/OpenCode/Tools/JsToolWorkflow.js')
    const toml = render(outcome)
    assert.equal(toml.includes('status = "ok"'), true)
    // renderString escapes inner quotes: result = "{\"before\":\"x\"}"
    assert.equal(toml.includes('result = "{\\"'), true)
    assert.equal(toml.includes('before'), true)
    assert.equal(toml.includes('written = "a.txt"'), true)
    // failed shape
    const failing = await workflowRun(dir, surface.BaseClassSource, `class Js extends JsProgram {
  async run() { throw new Error('boom'); }
}`, 2000, Date.now() + 60_000, 1 << 20)
    const failedToml = render(failing)
    assert.equal(failedToml.includes('status = "failed"'), true)
    assert.equal(failedToml.includes('code = "PROGRAM_FAILED"'), true)
  } finally {
    cleanup()
  }
})
