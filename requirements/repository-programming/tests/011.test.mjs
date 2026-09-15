import assert from 'node:assert/strict'
import test from 'node:test'
import { run } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync, existsSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { randomUUID } from 'node:crypto'
import { parse as parseToml } from 'smol-toml'
import { generate } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import {
import { create as createEventStore, dispose as disposeEventStore } from '../../../dist/Persistence/EventStore/Surface.js'
import { pending } from '../../../dist/Repository/Programming/Js/TransactionSurface.js'

// JS-011 sandbox authority and JS-054 deadline/output contracts through the
// registered runtime owner surface.



const BASE = `class JsProgram {
  constructor(api) { this._api = api; }
  file(path) { return this._api.js.read(path); }
}`

const runWrapped = async (
  program,
  api,
  { deadlineMs = 2000, outputBound = 1 << 20, deadlineEpochMs = Date.now() + 60_000 } = {},
) => run(BASE, program, api, deadlineMs, deadlineEpochMs, outputBound)

const failureCode = (result) => result.code

const PROGRAM = `class Js extends JsProgram {
  async run() {
    return { sum: 1 + 2, text: (await this.file('a.txt')).text };
  }
}`

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

test('WHAT[REPOSITORY-PROGRAMMING-011] JS010_circular_return_is_invalid_return_value', async () => {
  const circular = `class Js extends JsProgram {
  async run() { const a = {}; a.self = a; return a; }
}`
  const result = await runWrapped(circular, { js: {} })
  assert.equal(result.ok, false)
  assert.equal(failureCode(result), 'INVALID_RETURN_VALUE')
})

test('WHAT[REPOSITORY-PROGRAMMING-011] JS010_array_null_is_invalid_return_value', async () => {
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
    assert.equal(parseToml(render(outcome)).code, 'INVALID_RETURN_VALUE')
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
