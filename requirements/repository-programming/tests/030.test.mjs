import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, mkdtempSync, rmSync } from 'node:fs'
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

test('WHAT[repository-programming-030] a named park keeps one-shot handles and refuses a duplicate before effects', async () => {
  const { dir, cleanup } = sandbox()
  const session = 'sess-030-named'
  const invoke = (input, owner = session) =>
    execute(dir, 'Coder', 'en', owner, input, 2000, Date.now() + 60_000, 1 << 20, null)
  try {
    const first = await invoke({ name: 'review-030', program: program('await this.park({ step: 1 }); await this.park({ step: 2 });') })
    assert.equal(caseName(first), 'Parked', render(first))
    const firstHandle = parkedHandle(first)
    assert.match(firstHandle, /^review-030@\d+$/)
    assert.deepEqual(parkedPayload(first), { step: 1 })
    const duplicate = await invoke({ name: 'review-030', program: program('await this.write("duplicate-started", "bad");') })
    assert.equal(caseName(duplicate), 'Failed', render(duplicate))
    assert.equal(failureCode(duplicate), 'INVALID_ARGUMENT')
    assert.equal(existsSync(join(dir, 'duplicate-started')), false)
    const foreign = await invoke({ unpark: { handle: firstHandle, value: 'wrong owner' } }, 'sess-030-foreign')
    assert.equal(failureCode(foreign), 'HANDLE_UNKNOWN')
    const second = await invoke({ unpark: { handle: firstHandle, value: 'continue' } })
    assert.equal(caseName(second), 'Parked', render(second))
    const secondHandle = parkedHandle(second)
    assert.match(secondHandle, /^review-030@\d+$/)
    assert.notEqual(secondHandle, firstHandle)
    assert.deepEqual(parkedPayload(second), { step: 2 })
    assert.equal(failureCode(await invoke({ unpark: { handle: firstHandle, value: 'stale replay' } })), 'HANDLE_UNKNOWN')
    const done = await invoke({ unpark: { handle: secondHandle, value: 'finish' } })
    assert.equal(caseName(done), 'Succeeded', render(done))
    assert.equal(failureCode(await invoke({ unpark: { handle: secondHandle, value: 'duplicate' } })), 'HANDLE_UNKNOWN')
  } finally {
    clearParkedSites()
    cleanup()
  }
})

test('WHAT[repository-programming-030] an explicitly empty name refuses before program effects', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const result = await execute(dir, 'Coder', 'en', 'sess-030-empty-name',
      { name: ' ', program: program('await this.write("started", "bad");') },
      2000, Date.now() + 60_000, 1 << 20, null)
    assert.equal(failureCode(result), 'INVALID_ARGUMENT', render(result))
    assert.equal(existsSync(join(dir, 'started')), false)
  } finally {
    cleanup()
  }
})

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
    const blocked = await runProgram(dir, `    await this.deliver({ 0: "blocked report" });`, 'sess-030-a')
    assert.equal(caseName(blocked), 'Failed')
    assert.equal(failureCode(blocked), 'PROGRAM_FAILED')
    assert.match(failureReason(blocked), /delivery is blocked while this session has unresolved parked handles/)
    assert.match(failureReason(blocked), new RegExp(handle), 'refusal names the offending handle')
    assert.equal(deliveredValue('sess-030-a'), null, 'a blocked deliver never populates the session cell')

    // ...while session B's deliver goes through untouched by A's park.
    const other = await runProgram(dir, `    await this.deliver({ 0: "independent report" });`, 'sess-030-b')
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
      { program: program(`    await this.park({ reason: 'testing-lc-009' });`) },
      2000,
      Date.now() + 60_000,
      1 << 20,
      null,
    )
    assert.equal(caseName(parked), 'Parked')
    const handle = parkedHandle(parked)
    assert.ok(typeof handle === 'string' && handle.length > 0)

    // A foreign session cannot distinguish this handle from an unknown one.
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
    assert.equal(failureCode(unparkB), 'HANDLE_UNKNOWN')
    assert.doesNotMatch(failureReason(unparkB), /another session/)

    // Refused abandonment also hides the owner and leaves the handle intact.
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
    assert.equal(failureCode(abandonB), 'HANDLE_UNKNOWN')
    assert.doesNotMatch(failureReason(abandonB), /another session/)

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
