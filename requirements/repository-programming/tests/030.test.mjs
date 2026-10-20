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
  failureCode,
  failureReason,
  clearParkedSites,
  render,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { deliveredValue, clearDeliveredValue } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

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

test('WHAT[repository-programming-030] another session parking does not block this session deliver (LC-008)', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearParkedSites()
    clearDeliveredValue('sess-030-a')
    clearDeliveredValue('sess-030-b')

    // Session A parks and never resolves: the site stays in the table.
    const parked = await runProgram(dir, `    await this.park({ stage: 'review' });`, 'sess-030-a')
    assert.equal(caseName(parked), 'Parked')
    const handle = parkedHandle(parked)

    // LC-008: session A's own deliver is blocked by its own parked handle...
    const blocked = await runProgram(dir, `    await this.deliver({ final: true });`, 'sess-030-a')
    assert.equal(caseName(blocked), 'Failed')
    assert.equal(failureCode(blocked), 'PROGRAM_FAILED')
    assert.match(failureReason(blocked), /delivery is blocked while this session has unresolved parked handles/)
    assert.match(failureReason(blocked), new RegExp(handle), 'refusal names the offending handle')
    assert.equal(deliveredValue('sess-030-a'), null, 'a blocked deliver never populates the session cell')

    // ...while session B's deliver goes through untouched by A's park.
    const other = await runProgram(dir, `    await this.deliver({ prefixUntil: 0, thenMap: {} });`, 'sess-030-b')
    assert.equal(caseName(other), 'Succeeded')
  } finally {
    clearParkedSites()
    clearDeliveredValue('sess-030-a')
    clearDeliveredValue('sess-030-b')
    cleanup()
  }
})

test('WHAT[repository-programming-030] another session cannot unpark or abandon this session park (LC-009)', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearParkedSites()

    // Session A parks; session B must not be able to resolve A's site.
    const parked = await runProgram(dir, `    await this.park({ stage: 'review' });`, 'sess-030-a')
    assert.equal(caseName(parked), 'Parked')
    const handle = parkedHandle(parked)

    const crossUnpark = await execute(dir, 'Coder', 'en', 'sess-030-b', { unpark: { handle, value: { ok: true } } }, 2000, Date.now() + 60_000, 1 << 20, null)
    assert.equal(caseName(crossUnpark), 'Failed')
    assert.equal(failureCode(crossUnpark), 'HANDLE_UNKNOWN')
    assert.doesNotMatch(failureReason(crossUnpark), /another session/)

    const crossAbandon = await execute(dir, 'Coder', 'en', 'sess-030-b', { abandon: { handle } }, 2000, Date.now() + 60_000, 1 << 20, null)
    assert.equal(caseName(crossAbandon), 'Failed')
    assert.equal(failureCode(crossAbandon), 'HANDLE_UNKNOWN')
    assert.doesNotMatch(failureReason(crossAbandon), /another session/)

    // The foreign session's failed attempts leave A's site resolvable by A.
    const ownAbandon = await execute(dir, 'Coder', 'en', 'sess-030-a', { abandon: { handle } }, 2000, Date.now() + 60_000, 1 << 20, null)
    assert.equal(caseName(ownAbandon), 'Failed', 'abandon ends with Failed (Abandoned)')
  } finally {
    clearParkedSites()
    cleanup()
  }
})
