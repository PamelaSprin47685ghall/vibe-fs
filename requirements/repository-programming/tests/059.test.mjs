import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { execute, caseName } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import {
  deliveredValue, clearDeliveredValue, recordDeliveredView, recordFencedDeliveredView, takeDeliveredHandoff,
  takeDeliveredHandoffWith, takeDeliveredHandoffFor, clearDeliveredReturned, deliveredReturned,
  deliveredViewOf, deliveredFenceOf,
} from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { applyView, clearAll, numberMessage, setView } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'
import { verifyPrepareFailure, verifyCommitFailure } from './support/delivery-settlement.mjs'

test('WHAT[repository-programming-059] prepare failure leaves delivery empty and preserves actual file effects', verifyPrepareFailure)
test('WHAT[repository-programming-059] commit failure leaves delivery empty and preserves actual file effects', verifyCommitFailure)

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

test('WHAT[repository-programming-059] this.deliver records its view declaration and does not run later statements', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-view-deliver-'))
  const id = 'native-deliver'
  try {
    clearDeliveredValue(id)
    const result = await run(dir, 'await this.deliver({ prefixUntil: 0, thenMap: { 0: "indexed" } }); throw new Error("unreachable");', id)
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
