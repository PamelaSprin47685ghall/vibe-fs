import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { parse as parseToml } from 'smol-toml'
import { generate } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { createRegistered, execute } from '../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js'
import { run, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-host-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const toolModule = () => {
  const tool = (definition) => definition
  tool.schema = {
    string: () => ({
      type: 'string',
      describe: (description) => ({ type: 'string', description }),
    }),
  }
  return { tool }
}

const coderSurface = () => generate('Coder', ['Read', 'Write', 'Edit', 'Glob', 'Grep'], 'en')
const runWorkflow = async (dir, program, { deadlineMs = 2000, store = null } = {}) => ({
  outcome: await run(dir, 'Coder', 'en', program, deadlineMs, Date.now() + 60_000, 1 << 20, store),
  surface: coderSurface(),
})

test('WHAT[REPOSITORY-PROGRAMMING-016] JS073_spec_executes_program_and_renders_result', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello world', 'utf8')
    const registered = createRegistered(toolModule(), 'Coder', 'en', dir, null)

    const program = `class Js extends JsProgram {
  async run() {
    const view = await this.file('a.txt', [['begin', 'end', 'hello']]);
    this.rewrite('a.txt', view.text('^', 'begin') + 'goodbye' + view.text('end', '$'));
    return { before: view.text() };
  }
}`
    const result = await execute(registered, { program }, { sessionID: 'ses-test', agent: 'coder' })
    assert.equal(result.startsWith('# ok\n'), true)
    assert.equal(result.includes('[data]'), true)
    assert.equal(result.includes('before'), true)
    assert.equal(result.includes('[fs]'), true)
    assert.equal(result.includes('status ='), false)
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'goodbye world', 'committed via workflow')
    const missing = await execute(registered, {}, { sessionID: 'ses-test', agent: 'coder' })
    assert.equal(missing.includes('error'), true)
    assert.equal(missing.includes("missing 'program' argument") || missing.includes("缺少 'program' 参数"), true)
  } finally {
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
