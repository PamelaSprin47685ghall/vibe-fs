import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { execute, caseName } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import {
  deliveredValue, clearDeliveredValue, recordDeliveredView, takeDeliveredHandoff,
  takeDeliveredHandoffWith, clearDeliveredReturned, deliveredReturned,
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

test('WHAT[repository-programming-059] this.deliver records its terminal value and does not run later statements', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-view-deliver-'))
  const id = 'native-deliver'
  try {
    clearDeliveredValue(id)
    const result = await run(dir, 'await this.deliver({ summary: "indexed", files: 3 }); throw new Error("unreachable");', id)
    assert.equal(caseName(result), 'Succeeded')
    assert.equal(deliveredValue(id).summary, 'indexed')
    assert.equal(deliveredValue(id).files, 3)
  } finally { clearDeliveredValue(id); rmSync(dir, { recursive: true, force: true }) }
})

test('WHAT[repository-programming-059] handoff projects the complete view before dedup, including protected and null-kept content', () => {
  const id = 'handoff-complete'
  clearDeliveredReturned(id)
  const rows = seed(id, ['protected-one', 'raw-two', 'kept-three'])
  setView(id, declaration(2, { 2: 'summary-two' }))
  applyView(id, rows)
  recordDeliveredView(id, { files: 1 }, declaration(2, { 1: 'ignored', 2: 'summary-two', 3: null }))
  const first = String(takeDeliveredHandoff(id))
  assert.match(first, /protected-one/)
  assert.match(first, /summary-two/)
  assert.match(first, /kept-three/)
  assert.doesNotMatch(first, /raw-two|ignored/)
  assert.match(first, /files/)
  assert.deepEqual(deliveredReturned(id), ['1:13:protected-one', '2:11:summary-two', '3:10:kept-three'])
  seed(id, ['protected-one', 'raw-two', 'kept-three', 'new-four'])
  recordDeliveredView(id, { files: 2 }, declaration(99, {}))
  const second = String(takeDeliveredHandoff(id))
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
  recordDeliveredView(id, 'done', declaration(1, { 1: 'one-and-two-summary', 3: null }))
  const result = String(takeDeliveredHandoff(id))
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
  recordDeliveredView(id, null, declaration(0, { 0: 'opening-summary', 2: null }))
  const result = String(takeDeliveredHandoff(id))
  assert.match(result, /opening-summary/)
  assert.doesNotMatch(result, /summary-one/)
  assert.deepEqual(deliveredReturned(id), ['0:15:opening-summary', '2:3:two'])
  clearDeliveredReturned(id)
})

test('WHAT[repository-programming-059] without a view only the value returns; with a view assistant prose returns once', () => {
  const id = 'handoff-prose'
  clearDeliveredReturned(id)
  recordDeliveredView(id, 'terminal value', null)
  assert.equal(takeDeliveredHandoffWith(id, 'assistant prose'), 'terminal value')
  assert.deepEqual(deliveredReturned(id), [])
  recordDeliveredView(id, 'with view', declaration(1, {}))
  assert.match(String(takeDeliveredHandoffWith(id, 'assistant prose')), /assistant prose/)
  recordDeliveredView(id, 'next value', declaration(1, {}))
  assert.doesNotMatch(String(takeDeliveredHandoffWith(id, 'assistant prose')), /assistant prose/)
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
  recordDeliveredView(id, null, declaration(0, { 0: 'first' }))
  assert.match(String(takeDeliveredHandoff(id)), /first/)
  recordDeliveredView(id, null, declaration(0, { 0: 'second' }))
  assert.match(String(takeDeliveredHandoff(id)), /second/)
  recordDeliveredView(id, null, declaration(0, { 0: 'second' }))
  assert.doesNotMatch(String(takeDeliveredHandoff(id)), /second/)
  clearDeliveredReturned(id)
})
