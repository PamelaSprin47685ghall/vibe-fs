import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  execute,
  caseName,
  parkedHandle,
  failureCode,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-once-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

const runProgram = (dir, body, sessionId = 'sess-033') =>
  execute(dir, 'Coder', 'en', sessionId, { program: program(body) }, 2000, Date.now() + 60_000, 1 << 20, null)

const unpark = (dir, handle, value, sessionId = 'sess-033') =>
  execute(dir, 'Coder', 'en', sessionId, { unpark: { handle, value } }, 2000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-033] a handle is consumed exactly once: a second unpark reports HANDLE_UNKNOWN', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const parked = await runProgram(dir, `    const decision = await this.park({ stage: 'review' });
    return { decided: decision.value };`)
    assert.equal(caseName(parked), 'Parked')
    const handle = parkedHandle(parked)

    const first = await unpark(dir, handle, { go: 'ahead' })
    assert.equal(caseName(first), 'Succeeded', 'the first unpark resumes the program')

    const second = await unpark(dir, handle, { go: 'again' })
    assert.equal(caseName(second), 'Failed', 'a consumed handle must not resume twice')
    assert.equal(failureCode(second), 'HANDLE_UNKNOWN')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-033] abandon consumes the handle exactly once too', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const parked = await runProgram(dir, `    await this.park({ stage: 'review' });
    return { never: true };`)
    assert.equal(caseName(parked), 'Parked')
    const handle = parkedHandle(parked)

    const abandoned = await execute(dir, 'Coder', 'en', 'sess-033', { abandon: { handle } }, 2000, Date.now() + 60_000, 1 << 20, null)
    assert.equal(caseName(abandoned), 'Failed', 'abandon ends with a Failed outcome (Abandoned, no value)')

    const again = await unpark(dir, handle, { go: 'again' })
    assert.equal(caseName(again), 'Failed')
    assert.equal(failureCode(again), 'HANDLE_UNKNOWN')
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-033] abandon of an unknown handle reports HANDLE_UNKNOWN', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const abandoned = await execute(dir, 'Coder', 'en', 'sess-033', { abandon: { handle: 'park-424242' } }, 2000, Date.now() + 60_000, 1 << 20, null)
    assert.equal(caseName(abandoned), 'Failed')
    assert.equal(failureCode(abandoned), 'HANDLE_UNKNOWN')
  } finally {
    cleanup()
  }
})
