import assert from 'node:assert/strict'
import test from 'node:test'
import {
import { run } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync, existsSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { randomUUID } from 'node:crypto'
import { parse as parseToml } from 'smol-toml'
import { generate } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { create as createEventStore, dispose as disposeEventStore } from '../../../dist/Persistence/EventStore/Surface.js'
import { pending } from '../../../dist/Repository/Programming/Js/TransactionSurface.js'

// tests/unit/js-tools/js-anchors.test.mjs — G5 Phase B: failure algebra +
// ordered anchor declaration rules (JS-006/JS-019).
//
// JS-019: stable codes, frozen once shipped. AnchorRules owns the pure
// declaration refusals (empty anchor, non-positive occurrence); the other
// refusal classes live in the sandbox matcher / transaction layer.


  failureCatalog,
  validateAnchorDeclaration as validateDeclaration,
  validateAnchorOccurrence as validateOccurrence,
} from '../../../dist/Repository/Programming/Js/TransactionSurface.js'

const declaration = (spec, occurrence) => ({ ...spec, occurrence })
const ok = (result) => result.ok
const exact = (text) => ({ kind: 'exact', text })
const regex = (text) => ({ kind: 'regex', text })

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

test('WHAT[REPOSITORY-PROGRAMMING-018] JS019_failure_codes_are_stable_and_unique', () => {
  const expectedCodes = [
    'INVALID_PROGRAM',
    'PROGRAM_FAILED',
    'PROGRAM_TIMEOUT',
    'PROGRAM_RESOURCE_LIMIT',
    'FILE_NOT_FOUND',
    'FILE_ALREADY_EXISTS',
    'INVALID_UTF8',
    'ANCHOR_NOT_FOUND',
    'ANCHOR_NOT_UNIQUE',
    'INVALID_EDIT',
    'EDIT_NOT_FOUND',
    'EDIT_AMBIGUOUS',
    'EDIT_OVERLAP',
    'DUPLICATE_MUTATION_TARGET',
    'RESULT_TOO_LARGE',
    'INVALID_RETURN_VALUE',
    'FILE_CHANGED',
    'TRANSACTION_PREPARE_FAILED',
    'TRANSACTION_COMMIT_FAILED',
    'TRANSACTION_RECOVERY_REQUIRED',
    'UNKNOWN_MEMBER',
  ]
  const cases = failureCatalog()
  assert.equal(cases.length, expectedCodes.length)
  const seen = new Set()
  for (const expected of expectedCodes) {
    const entry = cases.find(({ code }) => code === expected)
    assert.ok(entry, `missing code ${expected}`)
    assert.equal(seen.has(entry.code), false, `duplicate code ${entry.code}`)
    seen.add(entry.code)
    assert.equal(typeof entry.reason, 'string')
    assert.ok(entry.reason.length > 0)
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-018] JS019_invalid_javascript_is_invalid_program', async () => {
  const bad = `class Js extends JsProgram { async run() { return { broken: } } }`
  const result = await runWrapped(bad, { js: {} })
  assert.equal(result.ok, false)
  assert.equal(failureCode(result), 'INVALID_PROGRAM')
})

test('WHAT[REPOSITORY-PROGRAMMING-018] JS019_program_throw_is_program_failed', async () => {
  const throwing = `class Js extends JsProgram {
  async run() { throw new Error('boom'); }
}`
  const result = await runWrapped(throwing, { js: {} })
  assert.equal(result.ok, false)
  assert.equal(failureCode(result), 'PROGRAM_FAILED')
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
