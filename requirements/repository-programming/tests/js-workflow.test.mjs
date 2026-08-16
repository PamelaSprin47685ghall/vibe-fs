// JS-085: sandbox → staging → preflight → commit is one owner-managed
// workflow. Result validation precedes commit and success is coupled to commit.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync, existsSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { randomUUID } from 'node:crypto'

import { parse as parseToml } from 'smol-toml'
import { generate } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import {
  run,
  caseName,
  rewritten,
  created,
  failureCode,
  render,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { create as createEventStore, dispose as disposeEventStore } from '../../../dist/Persistence/EventStore/Surface.js'
import { pending } from '../../../dist/Repository/Programming/Js/TransactionSurface.js'

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

test('WHAT[REPOSITORY-PROGRAMMING-019] JS085_workflow_preflight_blocks_stale_rewrite_without_touching_disk', async () => {
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

test('WHAT[REPOSITORY-PROGRAMMING-018] JS085_workflow_file_missing_fails_the_program', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const program = `class Js extends JsProgram {
  async run() {
    await this.file('missing.txt');
    return { ok: true };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    assert.equal(caseName(outcome), 'Failed')
    const failed = parseToml(render(outcome))
    assert.equal(failed.code, 'FILE_NOT_FOUND')
    assert.equal(failed.reason.includes('missing.txt'), true)
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-019] JS085_workflow_program_error_fails_without_commit', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'old', 'utf8')
    const program = `class Js extends JsProgram {
  async run() { throw new Error('boom'); }
}`
    const { outcome } = await runWorkflow(dir, program)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'PROGRAM_FAILED')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'old', 'no commit on program failure')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-012] JS012_workflow_with_store_persists_prepare_and_commit', async () => {
  const { dir, cleanup } = sandbox()
  const local = localStore()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    const file = await this.file('a.txt', [['begin', 'end', 'hello']]);
    this.rewrite('a.txt', file.text('^', 'begin') + 'goodbye' + file.text('end', '$'));
    return { done: true };
  }
}`
    const { outcome } = await runWorkflow(dir, program, { store: local.handle })
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'goodbye world', 'committed to disk')
    assert.deepEqual(pending(local.handle), [], 'no uncommitted transaction remains in Integrator Current')
  } finally {
    local.close()
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-016] JS016_result_renders_stable_toml_shapes', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    const file = await this.file('a.txt', [['begin', 'end', 'hello']]);
    this.rewrite('a.txt', file.text('^', 'begin') + 'goodbye' + file.text('end', '$'));
    return { before: 'x' };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    const toml = render(outcome)
    assert.equal(toml.startsWith('# ok\n'), true)
    assert.equal(/(?:^|\n)status =/m.test(toml), false)
    assert.equal(/(?:^|\n)result =/m.test(toml), false)
    assert.equal(/(?:^|\n)written =/m.test(toml), false)
    const doc = parseToml(toml)
    assert.equal(doc.data.before, 'x')
    assert.deepEqual(doc.fs.rewritten, ['a.txt'])
    assert.equal(doc.fs.created, undefined)
    const failing = await run(dir, 'Coder', 'en', `class Js extends JsProgram {
  async run() { throw new Error('boom'); }
}`, 2000, Date.now() + 60_000, 1 << 20, null)
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

test('WHAT[REPOSITORY-PROGRAMMING-016] JS010_016_query_object_has_data_and_no_fs', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const program = `class Js extends JsProgram {
  async run() { return { paths: ['a.txt'] } }
}`
    const { outcome } = await runWorkflow(dir, program)
    const toml = render(outcome)
    const doc = parseToml(toml)
    assert.deepEqual(doc.data.paths, ['a.txt'])
    assert.equal('truncated' in doc.data, false)
    assert.equal(doc.fs, undefined)
    assert.equal(toml.includes('[fs]'), false)
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-016] JS010_016_primitive_return_uses_data_field', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const { outcome } = await runWorkflow(dir, `class Js extends JsProgram {
  async run() { return 42 }
}`)
    const toml = render(outcome)
    assert.equal(parseToml(toml).data, 42)
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-011] JS010_array_null_is_invalid_return_value', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const program = `class Js extends JsProgram {
  async run() {
    this.rewrite('a.txt', 'new')
    return [null]
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(parseToml(render(outcome)).code, 'INVALID_RETURN_VALUE')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-019] JS019_invalid_return_value_commits_nothing', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'old', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    this.rewrite('a.txt', 'new')
    return [null]
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'old')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-011] JS010_mixed_object_array_is_invalid', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const { outcome } = await runWorkflow(dir, `class Js extends JsProgram {
  async run() { return [1, { a: 1 }] }
}`)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(parseToml(render(outcome)).code, 'INVALID_RETURN_VALUE')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-018] JS019_missing_anchor_uses_stable_code', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    await this.file('a.txt', [['begin', 'end', '## JS-007 FileView.text()']]);
    return { ok: true };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    const failed = parseToml(render(outcome))
    assert.equal(failed.code, 'ANCHOR_NOT_FOUND')
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-007] JS006_missing_anchor_reason_names_declaration_path_and_pattern', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    await this.file('a.txt', [['begin', 'end', '## JS-007 FileView.text()']]);
    return { ok: true };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    const failed = parseToml(render(outcome))
    assert.equal(failed.reason.includes('anchor 1'), true)
    assert.equal(failed.reason.includes('a.txt'), true)
    assert.equal(failed.reason.includes('## JS-007 FileView.text()'), true)
  } finally {
    cleanup()
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-007] JS005_offset_anchor_clips_to_closed_file_range', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
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
    const { outcome } = await runWorkflow(dir, program)
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

test('WHAT[REPOSITORY-PROGRAMMING-007] JS005_offset_N_is_string_index_not_line_number', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'ab\ncd\nef', 'utf8')
    const program = `class Js extends JsProgram {
  async run() {
    const file = await this.file('a.txt');
    return {
      twoUnits: file.text('^', '^+2'),
      threeLen: file.text('^', '^+3').length,
    };
  }
}`
    const { outcome } = await runWorkflow(dir, program)
    const doc = parseToml(render(outcome))
    assert.equal(doc.data.twoUnits, 'ab')
    assert.equal(doc.data.threeLen, 3)
  } finally {
    cleanup()
  }
})
