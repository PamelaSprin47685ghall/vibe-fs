import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, mkdirSync, writeFileSync, existsSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { run, caseName, failureCode, rewritten, created, removed, moved, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-rmmv-asis-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

// Engineer carries Move/Remove (office-capability); the generator resolves
// Coder through the same permission set, so Engineer is the explicit choice.
const execute = (dir, body) =>
  run(dir, 'Engineer', 'en', program(body), 2000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-042] rm landed before a later throw stays on disk and is reported in the failure [fs]', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'gone.tmp'), 'data', 'utf8')
    writeFileSync(join(dir, 'later.tmp'), 'later', 'utf8')
    const outcome = await execute(dir, `    this.rm('gone.tmp');
    this.rm('later.tmp');
    throw new Error('boom after removals');`)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'PROGRAM_FAILED')
    // As-is: both removals already landed; the failure reports them instead
    // of rolling them back (WHAT[029] failure shape extended to rm).
    assert.equal(existsSync(join(dir, 'gone.tmp')), false)
    assert.equal(existsSync(join(dir, 'later.tmp')), false)
    assert.deepEqual(removed(outcome), ['gone.tmp', 'later.tmp'])
    assert.deepEqual(rewritten(outcome), [])
    assert.deepEqual(created(outcome), [])
    assert.match(render(outcome), /removed = \[[^\]]*gone\.tmp[^\]]*later\.tmp[^\]]*\]/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-042] mv landed before a later throw stays on disk and the move is reported', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'origin.txt'), 'payload', 'utf8')
    const outcome = await execute(dir, `    this.mv('origin.txt', 'moved.txt');
    throw new Error('boom after move');`)
    assert.equal(caseName(outcome), 'Failed')
    assert.equal(failureCode(outcome), 'PROGRAM_FAILED')
    assert.equal(existsSync(join(dir, 'origin.txt')), false)
    assert.equal(existsSync(join(dir, 'moved.txt')), true)
    assert.deepEqual(moved(outcome), ['origin.txt -> moved.txt'])
    assert.match(render(outcome), /moved = \["origin\.txt -> moved\.txt"\]/)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-042] a failed rm keeps the target and reports an empty removed list', async () => {
  const { dir, cleanup } = sandbox()
  try {
    mkdirSync(join(dir, 'keep'))
    writeFileSync(join(dir, 'keep', 'a.txt'), 'a', 'utf8')
    const outcome = await execute(dir, `    this.rm('keep');
    throw new Error('boom after refused rm');`)
    assert.equal(caseName(outcome), 'Failed')
    // The rm itself refused with DIRECTORY_NOT_EMPTY, so the program's own
    // throw decides the top-level failure; nothing was removed.
    assert.equal(existsSync(join(dir, 'keep', 'a.txt')), true)
    assert.deepEqual(removed(outcome), [])
  } finally {
    cleanup()
  }
})
