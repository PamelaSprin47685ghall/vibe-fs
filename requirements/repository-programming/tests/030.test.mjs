import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  execute,
  caseName,
  parkedHandle,
  parkedPayload,
  render,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-park-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

const runProgram = (dir, body, sessionId = 'sess-030') =>
  execute(dir, 'Coder', 'en', sessionId, { program: program(body) }, 2000, Date.now() + 60_000, 1 << 20, null)

test('WHAT[repository-programming-030] a program that parks returns a top-level parked payload with handle and payload', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    const decision = await this.park({ stage: 'review' });
    return { decided: decision.value };`)
    assert.equal(caseName(outcome), 'Parked')
    assert.equal(typeof parkedHandle(outcome), 'string')
    assert.ok(parkedHandle(outcome).length > 0, 'handle must be a non-empty string')
    assert.deepEqual(parkedPayload(outcome), { stage: 'review' }, 'payload is the value the program passed to park')

    const doc = render(outcome)
    assert.equal(doc.startsWith('# parked'), true, 'parked renders as a top-level parked instruction')
    assert.match(doc, new RegExp(`handle = "${parkedHandle(outcome)}"`))
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-030] a program that does not park completes normally', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const outcome = await runProgram(dir, `    return { plain: true };`)
    assert.equal(caseName(outcome), 'Succeeded')
    assert.equal(parkedHandle(outcome), null)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-030] park is an async member that suspends at the await point', async () => {
  const { dir, cleanup } = sandbox()
  try {
    // The program parks twice in a row; the first park suspends the program
    // and the tool call returns that parked payload. The second park is only
    // reached after the first is resumed.
    const outcome = await runProgram(dir, `    const first = await this.park({ n: 1 });
    const second = await this.park({ n: 2 });
    return { first: first.value, second: second.value };`)
    assert.equal(caseName(outcome), 'Parked')
    assert.deepEqual(parkedPayload(outcome), { n: 1 }, 'the first park wins the race')
  } finally {
    cleanup()
  }
})
