import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, writeFileSync, existsSync, readFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  execute,
  caseName,
  parkedHandle,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-abandon-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

const runProgram = (dir, body, sessionId = 'sess-034') =>
  execute(dir, 'Coder', 'en', sessionId, { program: program(body) }, 2000, Date.now() + 60_000, 1 << 20, null)

const abandon = (dir, handle, sessionId = 'sess-034') =>
  execute(dir, 'Coder', 'en', sessionId, { abandon: { handle } }, 2000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-034] abandon rejects the parked await so native try/finally runs as a finalizer', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'resource.txt'), 'open', 'utf8')

    // The program parks inside a try, and its finally cleans up a resource.
    // Abandoning rejects the parked await; the finally must run and delete
    // the resource (as-is writes stay on disk).
    const body = `    try {
      await this.park({ stage: 'review' });
    } finally {
      this.rm('resource.txt');
    }
    return { survived: true };`
    const parked = await runProgram(dir, body)
    assert.equal(caseName(parked), 'Parked')

    const outcome = await abandon(dir, parkedHandle(parked))
    assert.equal(caseName(outcome), 'Failed', 'abandon ends with a Failed outcome (Abandoned, no value)')
    assert.equal(existsSync(join(dir, 'resource.txt')), false, 'the finally block ran and removed the resource')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-034] abandon cleanup can still use the api as-is', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'keep.txt'), 'keep', 'utf8')

    // The finally writes a cleanup marker through the api; the write must
    // land as-is even during the abandon cleanup window.
    const body = `    try {
      await this.park({ stage: 'review' });
    } finally {
      this.write('cleanup.txt', 'done');
    }
    return { survived: true };`
    const parked = await runProgram(dir, body)
    assert.equal(caseName(parked), 'Parked')

    await abandon(dir, parkedHandle(parked))
    assert.equal(readFileSync(join(dir, 'cleanup.txt'), 'utf8'), 'done', 'api writes land as-is during cleanup')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-034] a program that catches the abandonment and continues has its return value discarded', async () => {
  const { dir, cleanup } = sandbox()
  try {
    // The program catches the abandonment and returns a value anyway; the
    // outcome must still be Abandoned (Failed) with no value.
    const body = `    try {
      await this.park({ stage: 'review' });
    } catch (e) {
      // cleanup path: swallow and continue
    }
    return { swallowed: true };`
    const parked = await runProgram(dir, body)
    assert.equal(caseName(parked), 'Parked')

    const outcome = await abandon(dir, parkedHandle(parked))
    assert.equal(caseName(outcome), 'Failed', 'catching ABANDONED does not turn the outcome into Succeeded')
  } finally {
    cleanup()
  }
})
