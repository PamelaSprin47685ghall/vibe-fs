import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { execute, caseName } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import {
  deliveredValue, clearDeliveredValue, recordDeliveredView, recordFencedDeliveredView, takeDeliveredHandoff,
  takeDeliveredHandoffWith, takeDeliveredHandoffFor, clearDeliveredReturned, deliveredReturned,
  deliveredViewOf, deliveredFenceOf, takeDeliveredHandoffForCall,
} from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { applyView, clearAll, numberMessage, setView } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'

const program = (body) => 'class Js extends JsProgram { async run() { ' + body + ' } }'
const run = (dir, body, id) => execute(dir, 'Manager', 'en', id, { program: program(body) }, 3000, Date.now() + 60_000, 1 << 20, null)
const declaration = (prefixUntil, thenMap) => ({ prefixUntil, thenMap })
const seed = (id, texts) => {
  const rows = texts.map((text, index) => ({ id: 'message-' + index, text }))
  rows.forEach((row) => numberMessage(id, row.id))
  applyView(id, rows)
  return rows
}
test.beforeEach(() => clearAll())

test('WHAT[repository-programming-059] this.deliver records its report mapping as a prefixUntil=0 declaration and does not run later statements', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-view-deliver-'))
  const id = 'native-deliver'
  try {
    clearDeliveredValue(id)
    const result = await run(dir, 'await this.deliver({ 0: "indexed" }); throw new Error("unreachable");', id)
    assert.equal(caseName(result), 'Succeeded')
    assert.deepEqual(deliveredViewOf(id), { prefixUntil: 0, thenMap: { 0: 'indexed' } })
  } finally { clearDeliveredValue(id); rmSync(dir, { recursive: true, force: true }) }
})

test('WHAT[repository-programming-059] a fenced cell answers only its own provider run', () => {
  const id = 'fence-owner'
  clearDeliveredReturned(id)
  const rows = seed(id, ['protected-one', 'raw-two', 'kept-three'])
  setView(id, declaration(2, { 2: 'summary-two' }))
  applyView(id, rows)
  recordFencedDeliveredView(id, 'run-059', 'call-1', declaration(2, { 1: 'ignored', 2: 'summary-two', 3: null }))
  assert.deepEqual(deliveredFenceOf(id), { providerRun: 'run-059', toolCallId: 'call-1' })
  assert.deepEqual(deliveredViewOf(id), declaration(2, { 1: 'ignored', 2: 'summary-two', 3: null }))
  // A consumer from another run cannot take or peek the fenced cell.
  assert.equal(takeDeliveredHandoff(id), null)
  assert.equal(takeDeliveredHandoffFor(id, 'run-059-old', null), null)
  const first = String(takeDeliveredHandoffFor(id, 'run-059', null))
  assert.match(first, /protected-one/)
  assert.match(first, /summary-two/)
  assert.match(first, /kept-three/)
  assert.doesNotMatch(first, /raw-two|ignored/)
  assert.deepEqual(deliveredReturned(id), ['1:13:protected-one', '2:11:summary-two', '3:10:kept-three'])
  seed(id, ['protected-one', 'raw-two', 'kept-three', 'new-four'])
  recordFencedDeliveredView(id, 'run-059', 'call-2', declaration(99, {}))
  const second = String(takeDeliveredHandoffFor(id, 'run-059', null))
  assert.match(second, /new-four/)
  assert.doesNotMatch(second, /protected-one|raw-two|kept-three|summary-two/)
  assert.equal(deliveredValue(id), null)
  clearDeliveredReturned(id)
})

test('WHAT[repository-programming-059] permanent compression survives a later protected handoff and latest interval replacement', () => {
  const id = 'handoff-history'
  clearDeliveredReturned(id)
  const rows = seed(id, ['one', 'two', 'three'])
  setView(id, declaration(1, { 2: 'old-two-summary' }))
  applyView(id, rows)
  recordFencedDeliveredView(id, 'run-059', 'call-1', declaration(1, { 1: 'one-and-two-summary', 3: null }))
  const result = String(takeDeliveredHandoffFor(id, 'run-059', null))
  assert.match(result, /one-and-two-summary/)
  assert.match(result, /three/)
  assert.doesNotMatch(result, /old-two-summary/)
  assert.deepEqual(deliveredReturned(id), ['1:19:one-and-two-summary', '3:5:three'])
  clearDeliveredReturned(id)
})

test('WHAT[repository-programming-059] implicit zero can return the opening interval after anchor 1 disappears', () => {
  const id = 'handoff-opening'
  clearDeliveredReturned(id)
  const rows = seed(id, ['one', 'two'])
  setView(id, declaration(1, { 1: 'summary-one' }))
  applyView(id, rows)
  recordDeliveredView(id, declaration(0, { 0: 'opening-summary', 2: null }))
  const result = String(takeDeliveredHandoff(id))
  assert.match(result, /opening-summary/)
  assert.doesNotMatch(result, /summary-one/)
  assert.deepEqual(deliveredReturned(id), ['0:15:opening-summary', '2:3:two'])
  clearDeliveredReturned(id)
})

test('WHAT[repository-programming-059] handoff returns the assistant prose once and only through the owning run', () => {
  const id = 'handoff-prose'
  clearDeliveredReturned(id)
  recordFencedDeliveredView(id, 'run-059', 'call-1', declaration(1, {}))
  // An unfenced consumer sees nothing; the fenced cell stays in place.
  assert.equal(takeDeliveredHandoffWith(id, 'assistant prose'), null)
  assert.deepEqual(deliveredViewOf(id), declaration(1, {}), 'the cell survives the wrong consumer')
  const first = String(takeDeliveredHandoffFor(id, 'run-059', 'assistant prose'))
  assert.match(first, /assistant prose/)
  assert.equal(deliveredReturned(id).some((key) => key.startsWith('prose:')), true)
  recordFencedDeliveredView(id, 'run-059', 'call-2', declaration(1, {}))
  assert.doesNotMatch(String(takeDeliveredHandoffFor(id, 'run-059', 'assistant prose')), /assistant prose/)
  clearDeliveredReturned(id)
})

test('WHAT[repository-programming-059] an ordinary return does not populate the delivered cell', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-view-return-'))
  const id = 'ordinary-return'
  try {
    clearDeliveredValue(id)
    const result = await run(dir, 'return { plain: true };', id)
    assert.equal(caseName(result), 'Succeeded')
    assert.equal(deliveredValue(id), null)
  } finally { clearDeliveredValue(id); rmSync(dir, { recursive: true, force: true }) }
})

test('WHAT[repository-programming-059] changed opening material is returned once even though anchor zero remains', () => {
  const id = 'handoff-opening-version'
  clearDeliveredReturned(id)
  seed(id, ['one'])
  recordFencedDeliveredView(id, 'run-059', 'call-1', declaration(0, { 0: 'first' }))
  assert.match(String(takeDeliveredHandoffFor(id, 'run-059', null)), /first/)
  recordFencedDeliveredView(id, 'run-059', 'call-2', declaration(0, { 0: 'second' }))
  assert.match(String(takeDeliveredHandoffFor(id, 'run-059', null)), /second/)
  recordFencedDeliveredView(id, 'run-059', 'call-3', declaration(0, { 0: 'second' }))
  assert.doesNotMatch(String(takeDeliveredHandoffFor(id, 'run-059', null)), /second/)
  clearDeliveredReturned(id)
})

test('WHAT[repository-programming-059] a call of the same run cannot take the cell of another call of that run', () => {
  const id = 'handoff-same-run-two-calls'
  clearDeliveredReturned(id)
  seed(id, ['material-one', 'material-two'])
  // Two deliveries in ONE provider run: the second overwrites the cell.
  setView(id, declaration(2, { 1: 'first-call' }))
  recordFencedDeliveredView(id, 'run-059', 'call-1', declaration(2, { 1: 'first-call' }))
  setView(id, declaration(2, { 2: 'second-call' }))
  recordFencedDeliveredView(id, 'run-059', 'call-2', declaration(2, { 2: 'second-call' }))
  assert.deepEqual(deliveredFenceOf(id), { providerRun: 'run-059', toolCallId: 'call-2' })

  // The terminal of call-1 holds the whole fence of its own delivery. It
  // must not take the cell that call-2 left behind.
  const wrongTake = takeDeliveredHandoffForCall(id, 'run-059', 'call-1', null)
  assert.equal(wrongTake, null, 'a different call of the same run must not take this cell')
  assert.deepEqual(deliveredFenceOf(id), { providerRun: 'run-059', toolCallId: 'call-2' })
  assert.deepEqual(deliveredReturned(id), [], 'a refused take must not advance the returned cursor')

  // The owning call takes its own cell.
  const rightTake = takeDeliveredHandoffForCall(id, 'run-059', 'call-2', null)
  assert.match(String(rightTake), /second-call/)
  assert.equal(deliveredFenceOf(id), null)
  clearDeliveredReturned(id)
})

// Moved from tests/061.test.mjs (LC-006): the fenced delivered-view cell is [059]'s take/peek, and the requirement tree keeps one test file per clause.
{
// LC-006: 交付格按 provider run 实施精确围栏，防止旧终态错领新任务交付
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const {
  clearDeliveredValue,
  clearDeliveredReturned,
  recordFencedDeliveredView,
  takeDeliveredHandoffFor,
  deliveredFenceOf,
} = await import("../../../dist/Repository/Programming/Js/RuntimeSurface.js");
const { clearAll, numberMessage, applyView } = await import("../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js");


test('WHAT[repository-programming-059] LC-006: fenced delivered view answers only the owning run', () => {
  const session = 'session-lc006'
  const runA = 'run-A'
  const callA = 'call-A'
  const runB = 'run-B'
  const view = { prefixUntil: 1, thenMap: {} }

  clearDeliveredValue(session)
  clearDeliveredReturned(session)
  try {
    const rows = [{ id: 'msg-0', text: 'summary-text' }]
    rows.forEach((row) => numberMessage(session, row.id))
    applyView(session, rows)

    recordFencedDeliveredView(session, runA, callA, view)

    // runB 取交付必须返回 null，且 deliveredFenceOf 保持为 runA
    assert.equal(takeDeliveredHandoffFor(session, runB, null), null)
    assert.deepEqual(deliveredFenceOf(session), { providerRun: runA, toolCallId: callA })

    // runA 才能取走
    const handoffA = takeDeliveredHandoffFor(session, runA, null)
    assert.notEqual(handoffA, null)
    assert.match(String(handoffA), /summary-text/)

    // 取走后交付格已被消费
    assert.equal(deliveredFenceOf(session), null)
    assert.equal(takeDeliveredHandoffFor(session, runA, null), null)
  } finally {
    clearDeliveredValue(session)
    clearDeliveredReturned(session)
  }
})
}
