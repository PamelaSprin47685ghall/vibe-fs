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
import { parse as parseToml } from 'smol-toml'
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
    const view = await this.file('a.txt', [['begin', 'end', 'hello']]);
    this.rewrite('a.txt', view.text('^', 'begin') + 'goodbye' + view.text('end', '$'));
    return { before: view.text() };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.deepEqual(listItems(outcome.fields[1]), ['a.txt']) // rewritten
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
    this.rewrite('missing.txt', 'x');
    return { ok: true };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'current text')
  } finally {
    cleanup()
  }
})

test('JS085_workflow_file_missing_fails_the_program', async () => {
  const { dir, cleanup } = sandbox()
  try {
    // a missing target is a typed result the program can inspect (JS-019:
    // foreseeable failures are not exceptions)
    const program = `class Js extends JsProgram {
  async run() {
    await this.file('missing.txt');
    return { ok: true };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    assert.equal(caseName(outcome), 'Failed')
    const { JsToolsResult_render: render } = await import('../../../dist/Infrastructure/OpenCode/Tools/JsToolWorkflow.js')
    const failed = parseToml(render(outcome))
    assert.equal(failed.code, 'FILE_NOT_FOUND')
    assert.equal(failed.reason.includes('missing.txt'), true)
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
    const file = await this.file('a.txt', [['begin', 'end', 'hello']]);
    this.rewrite('a.txt', file.text('^', 'begin') + 'goodbye' + file.text('end', '$'));
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
    const file = await this.file('a.txt', [['begin', 'end', 'hello']]);
    this.rewrite('a.txt', file.text('^', 'begin') + 'goodbye' + file.text('end', '$'));
    return { before: 'x' };
  }
}`
    const outcome = await workflowRun(dir, surface.BaseClassSource, program, 2000, Date.now() + 60_000, 1 << 20)
    const { JsToolsResult_render: render } = await import('../../../dist/Infrastructure/OpenCode/Tools/JsToolWorkflow.js')
    const toml = render(outcome)
    assert.equal(toml.startsWith('# ok\n'), true)
    assert.equal(/(?:^|\n)status =/m.test(toml), false)
    assert.equal(/(?:^|\n)result =/m.test(toml), false)
    assert.equal(/(?:^|\n)written =/m.test(toml), false)
    const doc = parseToml(toml)
    assert.equal(doc.data.before, 'x')
    assert.deepEqual(doc.fs.rewritten, ['a.txt'])
    assert.equal(doc.fs.created, undefined)
    const failing = await workflowRun(dir, surface.BaseClassSource, `class Js extends JsProgram {
  async run() { throw new Error('boom'); }
}`, 2000, Date.now() + 60_000, 1 << 20)
    const failedToml = render(failing)
    assert.equal(failedToml.startsWith('# failed\n'), true)
    assert.equal(failedToml.includes('status ='), false)
    const failed = parseToml(failedToml)
    assert.equal(failed.code, 'PROGRAM_FAILED')
    assert.equal(typeof failed.reason, 'string')
    assert.equal(failed.reason.includes('boom'), true)
    assert.equal(failed.data, undefined)
    assert.equal(failed.fs, undefined)
  } finally {
    cleanup()
  }
})

test('JS010_016_query_object_has_data_and_no_fs', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const surface = generate('Coder', coderCaps)
    const program = `class Js extends JsProgram {
  async run() {
    return { paths: ['a.txt'], truncated: false }
  }
}`
    const outcome = await workflowRun(dir, surface.BaseClassSource, program, 2000, Date.now() + 60_000, 1 << 20)
    const { JsToolsResult_render: render } = await import('../../../dist/Infrastructure/OpenCode/Tools/JsToolWorkflow.js')
    const toml = render(outcome)
    const doc = parseToml(toml)
    assert.deepEqual(doc.data.paths, ['a.txt'])
    assert.equal(doc.data.truncated, false)
    assert.equal(doc.fs, undefined)
    assert.equal(toml.includes('[fs]'), false)
  } finally {
    cleanup()
  }
})

test('JS010_016_primitive_return_uses_data_field', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const surface = generate('Coder', coderCaps)
    const program = `class Js extends JsProgram {
  async run() { return 42 }
}`
    const outcome = await workflowRun(dir, surface.BaseClassSource, program, 2000, Date.now() + 60_000, 1 << 20)
    const { JsToolsResult_render: render } = await import('../../../dist/Infrastructure/OpenCode/Tools/JsToolWorkflow.js')
    const toml = render(outcome)
    assert.equal(parseToml(toml).data, 42)
  } finally {
    cleanup()
  }
})

test('JS010_array_null_is_invalid_and_does_not_commit', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'old', 'utf8')
    const surface = generate('Coder', coderCaps)
    const program = `class Js extends JsProgram {
  async run() {
    this.rewrite('a.txt', 'new')
    return [null]
  }
}`
    const outcome = await workflowRun(dir, surface.BaseClassSource, program, 2000, Date.now() + 60_000, 1 << 20)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'old')
    const { JsToolsResult_render: render } = await import('../../../dist/Infrastructure/OpenCode/Tools/JsToolWorkflow.js')
    const failed = parseToml(render(outcome))
    assert.equal(failed.code, 'INVALID_RETURN_VALUE')
  } finally {
    cleanup()
  }
})

test('JS010_mixed_object_array_is_invalid', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const surface = generate('Coder', coderCaps)
    const program = `class Js extends JsProgram {
  async run() { return [1, { a: 1 }] }
}`
    const outcome = await workflowRun(dir, surface.BaseClassSource, program, 2000, Date.now() + 60_000, 1 << 20)
    assert.equal(caseName(outcome), 'Failed')
    const { JsToolsResult_render: render } = await import('../../../dist/Infrastructure/OpenCode/Tools/JsToolWorkflow.js')
    assert.equal(parseToml(render(outcome)).code, 'INVALID_RETURN_VALUE')
  } finally {
    cleanup()
  }
})

test('JS006_019_missing_anchor_is_typed_and_names_the_pattern', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const surface = generate('Coder', coderCaps)
    const program = `class Js extends JsProgram {
  async run() {
    await this.file('a.txt', [['begin', 'end', '## JS-007 FileView.text()']]);
    return { ok: true };
  }
}`
    const outcome = await workflowRun(dir, surface.BaseClassSource, program, 2000, Date.now() + 60_000, 1 << 20)
    const { JsToolsResult_render: render } = await import('../../../dist/Infrastructure/OpenCode/Tools/JsToolWorkflow.js')
    const failed = parseToml(render(outcome))
    assert.equal(failed.code, 'ANCHOR_NOT_FOUND')
    assert.equal(failed.reason.includes('anchor 1'), true)
    assert.equal(failed.reason.includes('a.txt'), true)
    assert.equal(failed.reason.includes('## JS-007 FileView.text()'), true)
  } finally {
    cleanup()
  }
})

test('JS005_offset_anchor_clips_to_closed_file_range', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const surface = generate('Coder', coderCaps)
    const program = `class Js extends JsProgram {
  async run() {
    const file = await this.file('a.txt', [['h', 'hend', 'hello']]);
    return {
      window: file.text('h', 'h+6'),
      before: file.text('hend-5', 'hend'),
      clippedEnd: file.text('h', 'h+1000'),
      clippedStart: file.text('^-1000', 'h'),
      eof: file.text('$+100', '$+200').length,
    };
  }
}`
    const outcome = await workflowRun(dir, surface.BaseClassSource, program, 2000, Date.now() + 60_000, 1 << 20)
    const { JsToolsResult_render: render } = await import('../../../dist/Infrastructure/OpenCode/Tools/JsToolWorkflow.js')
    const doc = parseToml(render(outcome))
    assert.equal(doc.data.window, 'hello ')
    assert.equal(doc.data.before, 'hello')
    assert.equal(doc.data.clippedEnd, 'hello world')
    assert.equal(doc.data.clippedStart, '')
    assert.equal(doc.data.eof, 0)
  } finally {
    cleanup()
  }
})
