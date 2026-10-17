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
  runObserved,
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

test('WHAT[repository-programming-019] JS085_workflow_blocks_a_rewrite_of_a_missing_target', async () => {
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

test('WHAT[repository-programming-019] JS085_workflow_program_error_fails_without_commit', async () => {
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

test('WHAT[repository-programming-019] JS019_invalid_return_value_keeps_the_already_applied_rewrite', async () => {
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
    // Under as-is semantics the rewrite already landed before the invalid
    // return value was detected; the write stays on disk.
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'new')
  } finally {
    cleanup()
  }
})
