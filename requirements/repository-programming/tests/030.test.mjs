import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, existsSync, readFileSync } from 'node:fs'
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

let parkCount = 0
const runProgram = (dir, body, sessionId = 'sess-030', name = undefined) => {
  parkCount += 1
  const handleName = name || `park-${sessionId}-${parkCount}`
  return execute(dir, 'Coder', 'en', sessionId, { name: handleName, program: program(body) }, 2000, Date.now() + 60_000, 1 << 20, null)
}

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

    // Session A declares a custom pause: not on allow-list, so deliver is blocked with actionable choices.
    const parked = await runProgram(dir, `    await this.park({ note: 'x' });`, 'sess-030-a')
    assert.equal(caseName(parked), 'Parked')
    const handle = parkedHandle(parked)

    const blocked = await runProgram(dir, `    await this.deliver([]);`, 'sess-030-a')
    assert.equal(caseName(blocked), 'Failed')
    assert.equal(failureCode(blocked), 'PROGRAM_FAILED')
    assert.match(failureReason(blocked), /delivery is blocked by unresolved resources/)
    assert.match(failureReason(blocked), new RegExp(handle), 'refusal names the offending handle')
    assert.match(failureReason(blocked), /declared an unresolved pause of kind 'object'/)
    assert.match(failureReason(blocked), /resume with unpark\(\{ handle, value \}\) or cancel with abandon\(\{ handle \}\)/)

    // ...while session B's deliver goes through untouched by A's park.
    const other = await runProgram(dir, `    await this.deliver([]);`, 'sess-030-b')
    assert.equal(caseName(other), 'Succeeded')
  } finally {
    clearParkedSites()
    clearDeliveredValue('sess-030-a')
    clearDeliveredValue('sess-030-b')
    cleanup()
  }
})

test('WHAT[repository-programming-030] auto-settle observes finally cleanup of abandoned continuation', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearParkedSites()
    clearDeliveredValue('sess-030-cleanup')

    // Program parks with provably side-effect-free runtime marker inside a try/finally block
    const parked = await runProgram(dir, `
    try {
      await this.park({ __jsKnownNoEffect: true, reason: 'checkpoint' });
    } finally {
      await this.write('cleanup.marker', 'cleaned-up-ok');
    }
    `, 'sess-030-cleanup')
    assert.equal(caseName(parked), 'Parked')
    assert.equal(existsSync(join(dir, 'cleanup.marker')), false, 'cleanup marker not yet written while parked')

    // Delivering auto-settles the side-effect-free pause: continuation is abandoned and finally runs
    const outcome = await runProgram(dir, `    await this.deliver([]);`, 'sess-030-cleanup')
    assert.equal(caseName(outcome), 'Succeeded', 'deliver succeeds without manual intervention')
    assert.equal(existsSync(join(dir, 'cleanup.marker')), true, 'finally block cleanup was observed on disk')
    assert.equal(readFileSync(join(dir, 'cleanup.marker'), 'utf8'), 'cleaned-up-ok')
  } finally {
    clearParkedSites()
    clearDeliveredValue('sess-030-cleanup')
    cleanup()
  }
})

test('WHAT[repository-programming-030] where ambiguity pause is blocked because continuation effects cannot be proven', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearParkedSites()
    clearDeliveredValue('sess-030-where')

    // Program pauses on where ambiguity (2 occurrences of "foo")
    const parked = await runProgram(dir, `    const span = await this.where('foo bar foo', 'foo');`, 'sess-030-where')
    assert.equal(caseName(parked), 'Parked')

    const blocked = await runProgram(dir, `    await this.deliver([]);`, 'sess-030-where')
    assert.equal(caseName(blocked), 'Failed')
    assert.equal(failureCode(blocked), 'PROGRAM_FAILED')
    assert.match(failureReason(blocked), /delivery is blocked by unresolved resources/)
    assert.match(failureReason(blocked), /is paused on where ambiguity search for 'foo'/)
    assert.match(failureReason(blocked), /resolve with unpark\(\{ handle, value \}\) or cancel with abandon\(\{ handle \}\)/)
  } finally {
    clearParkedSites()
    clearDeliveredValue('sess-030-where')
    cleanup()
  }
})

test('WHAT[repository-programming-030] command yield pause holds active resources and blocks deliver with actionable choice', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearParkedSites()
    clearDeliveredValue('sess-030-yield')

    const parked = await runProgram(dir, `    await this.park({ __jsYield: true, timeoutMs: 5000 });`, 'sess-030-yield')
    assert.equal(caseName(parked), 'Parked')
    const handle = parkedHandle(parked)

    const blocked = await runProgram(dir, `    await this.deliver([]);`, 'sess-030-yield')
    assert.equal(caseName(blocked), 'Failed')
    assert.equal(failureCode(blocked), 'PROGRAM_FAILED')
    assert.match(failureReason(blocked), /delivery is blocked by unresolved resources/)
    assert.match(failureReason(blocked), new RegExp(handle))
    assert.match(failureReason(blocked), /holds an active background command/)
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
    assert.equal(failureCode(crossUnpark), 'INVALID_ARGUMENT')
    assert.match(failureReason(crossUnpark), /the paused program belongs to another session/)

    const crossAbandon = await execute(dir, 'Coder', 'en', 'sess-030-b', { abandon: { handle } }, 2000, Date.now() + 60_000, 1 << 20, null)
    assert.equal(caseName(crossAbandon), 'Failed')
    assert.equal(failureCode(crossAbandon), 'INVALID_ARGUMENT')
    assert.match(failureReason(crossAbandon), /the paused program belongs to another session/)

    // The foreign session's failed attempts leave A's site resolvable by A.
    const ownAbandon = await execute(dir, 'Coder', 'en', 'sess-030-a', { abandon: { handle } }, 2000, Date.now() + 60_000, 1 << 20, null)
    assert.equal(caseName(ownAbandon), 'Failed', 'abandon ends with Failed (Abandoned)')
  } finally {
    clearParkedSites()
    cleanup()
  }
})

// Moved from tests/062.test.mjs (LC-009): cross-session unpark/abandon refusal is [030]'s park ownership; one test file per clause.
{
// LC-009: 跨 session unpark/abandon 互斥与拒绝守卫
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const {
  execute,
  caseName,
  parkedHandle,
  failureCode,
  failureReason,
  clearParkedSites,
} = await import("../../../dist/Repository/Programming/Js/WorkflowSurface.js");

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-lc009-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
    return { done: true };
  }
}`

test('WHAT[repository-programming-030] session A park handle cannot be unparked or abandoned by session B, but A can abandon', async () => {
  const { dir, cleanup } = sandbox()
  try {
    clearParkedSites()

    // Session A parks and gets handle
    const parked = await execute(
      dir,
      'Coder',
      'en',
      'sess-lc009-a',
      { name: 'handle-lc009-a', program: program(`    await this.park({ reason: 'testing-lc-009' });`) },
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(parked), 'Parked')
    const handle = parkedHandle(parked)
    assert.ok(typeof handle === 'string' && handle.length > 0)

    // Session B unpark must fail with another session refusal
    const unparkB = await execute(
      dir,
      'Coder',
      'en',
      'sess-lc009-b',
      { unpark: { handle, value: { approved: true } } },
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(unparkB), 'Failed')
    assert.equal(failureCode(unparkB), 'INVALID_ARGUMENT')
    assert.match(failureReason(unparkB), /another session/)

    // Session B abandon must fail with another session refusal
    const abandonB = await execute(
      dir,
      'Coder',
      'en',
      'sess-lc009-b',
      { abandon: { handle } },
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(abandonB), 'Failed')
    assert.equal(failureCode(abandonB), 'INVALID_ARGUMENT')
    assert.match(failureReason(abandonB), /another session/)

    // Session A itself can still abandon its own handle
    const abandonA = await execute(
      dir,
      'Coder',
      'en',
      'sess-lc009-a',
      { abandon: { handle } },
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(abandonA), 'Failed')
    assert.equal(failureCode(abandonA), 'ABANDONED')
  } finally {
    clearParkedSites()
    cleanup()
  }
})
}
