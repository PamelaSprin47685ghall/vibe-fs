import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { parse as parseToml } from 'smol-toml'
import { execute, caseName } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import {
  deliveredValue, clearDeliveredValue, recordDeliveredView, recordFencedDeliveredView,
  takeDeliveredHandoff, takeDeliveredHandoffWith, deliveredCellIsMine,
  clearDeliveredReturned, deliveredReturned, deliveredViewOf, deliveredFenceOf,
  takeDeliveredHandoffForCall,
} from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { applyView, clearAll, numberMessage } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'

const program = (body) => 'class Js extends JsProgram { async run() { ' + body + ' } }'
const run = (dir, body, id) => execute(dir, 'Manager', 'en', id, { name: 'delivery-material', program: program(body) }, 3000, Date.now() + 60_000, 1 << 20, null)
const texts = (toml) => toml === null ? [] : (parseToml(String(toml)).returned || []).map((piece) => piece.text)
const seed = (id, values) => {
  const rows = values.map((text, index) => ({ id: 'message-' + index, text }))
  rows.forEach((row) => numberMessage(id, row.id))
  applyView(id, rows)
}

test.beforeEach(() => clearAll())

test('WHAT[repository-programming-059] deliver stores its carry array and ends the program', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-carry-deliver-'))
  const id = 'native-deliver'
  try {
    clearDeliveredValue(id)
    const result = await run(dir, 'await this.deliver(["indexed"]); throw new Error("unreachable");', id)
    assert.equal(caseName(result), 'Succeeded')
    assert.deepEqual(deliveredViewOf(id), ['indexed'])
  } finally {
    clearDeliveredValue(id)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[repository-programming-059] a fenced cell answers only its own call', () => {
  const id = 'fence-owner'
  clearDeliveredReturned(id)
  seed(id, ['first evidence', 'second evidence', 'unselected evidence'])
  recordFencedDeliveredView(id, 'run-059', 'call-1', [2, 'literal summary', 1])
  assert.deepEqual(deliveredFenceOf(id), { providerRun: 'run-059', toolCallId: 'call-1' })
  assert.deepEqual(deliveredViewOf(id), [2, 'literal summary', 1])
  assert.equal(takeDeliveredHandoff(id), null)
  assert.equal(deliveredCellIsMine(id, 'run-059', 'call-1'), true)
  assert.equal(deliveredCellIsMine(id, 'run-059', 'call-2'), false)
  assert.equal(takeDeliveredHandoffForCall(id, 'run-old', 'call-1', null), null)
  assert.equal(takeDeliveredHandoffForCall(id, 'run-059', 'call-2', null), null)
  assert.deepEqual(deliveredReturned(id), [])
  assert.deepEqual(texts(takeDeliveredHandoffForCall(id, 'run-059', 'call-1', null)), ['second evidence', 'literal summary', 'first evidence'])
  assert.equal(deliveredValue(id), null)
  clearDeliveredReturned(id)
})

test('WHAT[repository-programming-059] carry selects history in array order without an implicit prefix', () => {
  const id = 'ordered-carry'
  clearDeliveredReturned(id)
  seed(id, ['one', 'two', 'three'])
  recordDeliveredView(id, [3, 'between', 1, 3])
  assert.deepEqual(texts(takeDeliveredHandoff(id)), ['three', 'between', 'one', 'three'])
  clearDeliveredReturned(id)
})

test('WHAT[repository-programming-059] the last report returns without selecting any old context', () => {
  const id = 'report-only'
  clearDeliveredReturned(id)
  seed(id, ['old evidence', 'old conclusion'])
  recordFencedDeliveredView(id, 'run-report', 'call-report', [])
  assert.equal(takeDeliveredHandoffWith(id, 'final report'), null)
  assert.deepEqual(deliveredViewOf(id), [])
  assert.deepEqual(texts(takeDeliveredHandoffForCall(id, 'run-report', 'call-report', 'final report')), ['final report'])
  assert.equal(takeDeliveredHandoffForCall(id, 'run-report', 'call-report', 'final report'), null)
  clearDeliveredReturned(id)
})

test('WHAT[repository-programming-059] each delivery retains its current report even when the words are unchanged', () => {
  const id = 'current-report-identity'
  clearDeliveredReturned(id)
  for (const call of ['first', 'second']) {
    recordFencedDeliveredView(id, 'run-report', call, [])
    assert.deepEqual(texts(takeDeliveredHandoffForCall(id, 'run-report', call, 'same report')), ['same report'])
  }
  assert.equal(deliveredReturned(id).filter((key) => key.startsWith('turn:')).length, 2)
  clearDeliveredReturned(id)
})

test('WHAT[repository-programming-059] equal report lengths and prefixes never hide changed endings', () => {
  const id = 'report-prefix-collision'
  clearDeliveredReturned(id)
  const prefix = 'the shared twenty-four character report prefix: '
  for (const [call, report] of [['first', prefix + 'passed'], ['second', prefix + 'failed']]) {
    recordFencedDeliveredView(id, 'run-report', call, [])
    assert.deepEqual(texts(takeDeliveredHandoffForCall(id, 'run-report', call, report)), [report])
  }
  clearDeliveredReturned(id)
})

test('WHAT[repository-programming-059] selected history and current report form actual pretty TOML', () => {
  const id = 'pretty-return'
  clearDeliveredReturned(id)
  const literal = 'old evidence\nquoted "value"\npath = C:\\tmp'
  const report = '最终报告\n完成第一项。\n第二项仍待处理。'
  recordFencedDeliveredView(id, 'run-toml', 'call-toml', [literal])
  const result = String(takeDeliveredHandoffForCall(id, 'run-toml', 'call-toml', report))
  assert.deepEqual(texts(result), [literal, report])
  assert.equal((result.match(/\[\[returned\]\]/g) || []).length, 2)
  assert.ok(result.includes('old evidence\n'), 'the multiline text is printed across real lines')
  assert.ok(result.includes('最终报告\n'), 'the report is printed across real lines')
  assert.equal(result.split('old evidence').length - 1, 1, 'cursor keys do not repeat the material')
  assert.equal(result.split('最终报告').length - 1, 1, 'the current report appears once')
  clearDeliveredReturned(id)
})

test('WHAT[repository-programming-059] unchanged old material returns once while changed literals remain visible', () => {
  const id = 'history-cursor'
  clearDeliveredReturned(id)
  recordFencedDeliveredView(id, 'run', 'first', ['old', 'first version'])
  assert.deepEqual(texts(takeDeliveredHandoffForCall(id, 'run', 'first', null)), ['old', 'first version'])
  recordFencedDeliveredView(id, 'run', 'second', ['old', 'second version'])
  assert.deepEqual(texts(takeDeliveredHandoffForCall(id, 'run', 'second', null)), ['second version'])
  recordFencedDeliveredView(id, 'run', 'third', ['second version'])
  assert.deepEqual(texts(takeDeliveredHandoffForCall(id, 'run', 'third', null)), [])
  assert.equal(deliveredValue(id), null)
  clearDeliveredReturned(id)
})

test('WHAT[repository-programming-059] an ordinary return does not establish delivery', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-carry-return-'))
  const id = 'ordinary-return'
  try {
    clearDeliveredValue(id)
    const result = await run(dir, 'return { plain: true };', id)
    assert.equal(caseName(result), 'Succeeded')
    assert.equal(deliveredValue(id), null)
  } finally {
    clearDeliveredValue(id)
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[repository-programming-059] another call in the same run cannot consume the delivery', () => {
  const id = 'same-run-two-calls'
  clearDeliveredReturned(id)
  recordFencedDeliveredView(id, 'run-059', 'call-1', ['first call'])
  recordFencedDeliveredView(id, 'run-059', 'call-2', ['second call'])
  assert.equal(takeDeliveredHandoffForCall(id, 'run-059', 'call-1', 'first report'), null)
  assert.deepEqual(deliveredFenceOf(id), { providerRun: 'run-059', toolCallId: 'call-2' })
  assert.deepEqual(deliveredReturned(id), [])
  assert.deepEqual(texts(takeDeliveredHandoffForCall(id, 'run-059', 'call-2', 'second report')), ['second call', 'second report'])
  assert.equal(deliveredFenceOf(id), null)
  clearDeliveredReturned(id)
})
