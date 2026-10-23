import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import {
  anchors, applyView, clearAll, countTokens, numberMessage,
  captureView, takeView, carryMaterial, setView,
} from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'
import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'

const session = 'carry-compression'
const input = (...texts) => texts.map((text, index) => ({ id: String(index + 1), text }))
const declaration = (prefixUntil, carry) => ({ prefixUntil, carry })
const declare = (prefixUntil, carry) => {
  const result = captureView(session, declaration(prefixUntil, carry))
  assert.equal(result.ok, true, JSON.stringify(result))
}
const declareForCall = (messageId, callId, prefixUntil, carry) => {
  const result = setView(session, [messageId, callId], declaration(prefixUntil, carry))
  assert.ok(result, String(result))
}
const render = (rows) => applyView(session, rows).entries.map((entry) =>
  entry.kind === 'kept' ? entry.message.text : entry.text)
const user = (id, text) => ({ info: { id, role: 'user' }, parts: [{ type: 'text', text }] })
const assistant = (id, parts) => ({ info: { id, role: 'assistant' }, parts })
const tool = (callID, input, output, status = 'completed') => ({
  type: 'tool', tool: 'todowrite-for-engineer', callID, state: { status, input, output },
})
const allText = (messages) => JSON.stringify(messages)
test.beforeEach(() => clearAll())

test('WHAT[context-compression-031] anchor identities remain stable and declaration notification is consumed once', () => {
  const rows = input('one', 'two')
  render(rows)
  assert.equal(numberMessage(session, '1'), 1)
  assert.equal(numberMessage(session, '1'), 1)
  declare(2, [2])
  assert.deepEqual(takeView(session), declaration(2, [2]))
  assert.equal(takeView(session), null)
  assert.deepEqual(render(rows), ['one', 'two'])
})

test('WHAT[context-compression-031] prefixUntil keeps anchors strictly less than the cutoff', () => {
  numberMessage('other-session', 'gap')
  const rows = input('one', 'two', 'three')
  render(rows)
  const ids = anchors(session).map((row) => row.anchor)
  assert.deepEqual(ids, [2, 3, 4])
  // keep anchors < 4 → 2 and 3; carry references the third visible anchor
  declare(4, ['replacement', ids[2]])
  assert.deepEqual(render(rows), ['one', 'two', 'replacement', 'three'])
})

test('WHAT[context-compression-031] carry preserves order, repeated references, numeric strings, and empty literals', () => {
  const rows = input('one', 'two', 'three')
  render(rows)
  declare(0, [3, '3', 1, '', '  ', 3])
  assert.deepEqual(render(rows), ['three', '3', 'one', '', '  ', 'three'])
  assert.deepEqual(render(rows), ['three', '3', 'one', '', '  ', 'three'])
})

test('WHAT[context-compression-031] unknown anchors and malformed declarations leave the last valid declaration intact', () => {
  const rows = input('one', 'two')
  render(rows)
  const bad = [
    {}, { prefixUntil: 0 }, { carry: [] }, { prefixUntil: '0', carry: [] },
    { prefixUntil: -1, carry: [] }, { prefixUntil: 0.5, carry: [] },
    { prefixUntil: Number.MAX_SAFE_INTEGER + 1, carry: [] },
    { prefixCount: 0, carry: [] },
    { prefixUntil: 0, carry: {} }, { prefixUntil: 0, carry: null },
    { prefixUntil: 0, carry: [null] }, { prefixUntil: 0, carry: [true] },
    { prefixUntil: 0, carry: [-1] }, { prefixUntil: 0, carry: [1.5] },
    { prefixUntil: 0, carry: [NaN] }, { prefixUntil: 0, carry: [Infinity] },
    { prefixUntil: 0, carry: [999] }, { prefixUntil: 0, carry: [[]] },
    { prefixUntil: 0, carry: [{ toJSON() { return 'fake' } }] },
    { prefixUntil: 0, carry: [], thenMap: {} },
  ]
  for (const value of bad) {
    declare(0, ['last valid'])
    assert.equal(captureView(session, value).ok, false, JSON.stringify(value))
    assert.deepEqual(takeView(session), declaration(0, ['last valid']))
  }
  assert.deepEqual(render(rows), ['last valid'])
})

test('WHAT[context-compression-031] the declaration takes a value snapshot', () => {
  const rows = input('old')
  render(rows)
  const carry = ['saved']
  declare(0, carry)
  carry[0] = 'mutated'
  carry.push('extra')
  assert.deepEqual(render(rows), ['saved'])
})

test('WHAT[context-compression-031] the current turn is outside the selectable history and always survives', () => {
  const rows = input('old-one', 'old-two')
  render(rows)
  assert.equal(carryMaterial(session, [3]).ok, false)
  declare(0, [])
  const expanded = [...rows, { id: '3', text: 'current report' }, { text: 'current tool result' }]
  assert.deepEqual(render(expanded), ['current report', 'current tool result'])
  assert.deepEqual(render(expanded), ['current report', 'current tool result'])
  assert.equal(carryMaterial(session, [1]).ok, false, 'discarded history cannot be selected again')
})

test('WHAT[context-compression-031] later history cannot resurrect discarded content', () => {
  const rows = input('one', 'two', 'three')
  render(rows)
  declare(0, [2, 'summary'])
  assert.deepEqual(render(rows), ['two', 'summary'])
  assert.equal(captureView(session, declaration(0, [1])).ok, false)
  const expanded = [...rows, { id: '4', text: 'new evidence' }]
  assert.deepEqual(render(expanded), ['two', 'summary', 'new evidence'])
  declare(6, [])
  assert.deepEqual(render(expanded), ['two', 'summary', 'new evidence'])
})

test('WHAT[context-compression-031] literal segments receive selectable stable anchors', () => {
  const rows = input('old')
  render(rows)
  declare(0, ['literal'])
  render(rows)
  const id = anchors(session)[0].anchor
  assert.deepEqual(carryMaterial(session, [id]).material.map((piece) => piece.text), ['literal'])
  declare(0, [id, id])
  assert.deepEqual(render(rows), ['literal', 'literal'])
  assert.deepEqual(anchors(session).map((row) => row.anchor), [id, id])
})

test('WHAT[context-compression-031] the implicit opening counts only when it contains messages', () => {
  const rows = [{ text: 'opening' }, ...input('one', 'two')]
  render(rows)
  assert.deepEqual(anchors(session).map((row) => row.anchor), [0, 1, 2])
  declare(1, [2])
  assert.deepEqual(render(rows), ['opening', 'two'])
  clearAll()
  render(input('one'))
  assert.equal(carryMaterial(session, [0]).ok, false, 'an empty implicit opening is not selectable')
})

test('WHAT[context-compression-031] token accounting measures rendered material and the preserved prefix', () => {
  const rows = input('one', 'two')
  render(rows)
  declare(2, ['short'])
  const result = applyView(session, rows)
  assert.equal(result.report.total, countTokens('one') + countTokens('two'))
  assert.equal(result.report.used, countTokens('one') + countTokens('short'))
  assert.equal(result.report.cached, countTokens('one'))
  assert.equal(result.report.prefixCount, 1)
})

test('WHAT[context-compression-031] current prose appended to a kept segment is not counted as cached', () => {
  const rows = input('kept history')
  render(rows)
  declare(2, [])
  const result = applyView(session, [...rows, { text: 'new prose outside the snapshot' }])
  assert.equal(result.report.cached, countTokens('kept history'))
  assert.equal(result.report.used, countTokens('kept history') + countTokens('new prose outside the snapshot'))
})

test('WHAT[context-compression-031] transfer projection neither consumes the pending declaration nor edits history', () => {
  const rows = input('one', 'two')
  render(rows)
  declare(0, ['compressed'])
  assert.deepEqual(carryMaterial(session, [2, 'extra', 1]).material.map((piece) => piece.text), ['two', 'extra', 'one'])
  assert.deepEqual(takeView(session), declaration(0, ['compressed']))
  assert.deepEqual(render(rows), ['compressed'])
})

test('WHAT[context-compression-031] marker positions expose prefixCount and raw source remains unchanged', () => {
  const raw = [
    user('u', 'question'),
    assistant('a', [{ type: 'text', text: 'answer' }]),
    assistant('r', [tool('read-1', {}, 'result')]),
  ]
  const saved = structuredClone(raw)
  const first = pair.applyAnchorView(session, raw)
  assert.equal(first.messages[0].parts[0].text, '[ANCHOR: 1; prefixCount: 1]\0\uFEFFquestion')
  assert.deepEqual(first.messages[1], raw[1])
  assert.equal(first.messages[2].parts[0].state.output, '[ANCHOR: 2; prefixCount: 2]\0\uFEFFresult')
  assert.deepEqual(raw, saved)
  declare(3, [])
  const next = pair.applyAnchorView(session, [...raw, user('u2', 'next')])
  assert.deepEqual(next.messages.slice(0, raw.length), first.messages)
  assert.match(next.report, /prefixUntil=3/)
})

test('WHAT[context-compression-031] repeated anchors have distinct prefix positions', () => {
  const raw = [user('one', 'one'), user('two', 'two')]
  pair.applyAnchorView(session, raw)
  declare(0, [2, 1, 2])
  const result = pair.applyAnchorView(session, raw)
  assert.deepEqual(result.messages.map((row) => row.parts[0].text), [
    '[ANCHOR: 2; prefixCount: 1]\0\uFEFFtwo',
    '[ANCHOR: 1; prefixCount: 2]\0\uFEFFone',
    '[ANCHOR: 2; prefixCount: 3]\0\uFEFFtwo',
  ])
})

test('WHAT[context-compression-031] repeated history references cannot acquire different current prose', () => {
  const raw = [user('one', 'original evidence')]
  pair.applyAnchorView(session, raw)
  declare(0, [1, 1])
  const expanded = [...raw, assistant('current', [{ type: 'text', text: 'new independent report' }])]
  const projected = pair.applyAnchorView(session, expanded)
  const visible = anchors(session)
  assert.equal(visible.length, 3)
  assert.deepEqual(visible.slice(0, 2).map((row) => row.anchor), [1, 1])
  const currentAnchor = visible[2].anchor
  assert.notEqual(currentAnchor, 1)
  assert.deepEqual(carryMaterial(session, [1]).material.map((piece) => piece.text), ['original evidence'])
  assert.deepEqual(carryMaterial(session, [currentAnchor]).material.map((piece) => piece.text), ['new independent report'])
  assert.match(projected.messages[2].parts[0].text, new RegExp(`ANCHOR: ${currentAnchor}; prefixCount: 3`))
  const replay = pair.applyAnchorView(session, expanded)
  assert.deepEqual(replay.messages, projected.messages)
})

test('WHAT[context-compression-031] successful compression strips only its retained carry arguments and preserves its report once', () => {
  const raw = [user('old', 'old evidence')]
  pair.applyAnchorView(session, raw)
  const literal = 'LARGE-EXACT-LITERAL-' + 'payload'.repeat(80)
  declareForCall('current', 'compress-call', 0, [literal])
  const current = assistant('current', [
    { type: 'text', text: 'FINAL REPORT KEPT NATURALLY' },
    tool('compress-call', { program: 'return true;', prefixUntil: 0, carry: [literal] }, 'ok'),
  ])
  const expanded = [...raw, current]
  const saved = structuredClone(expanded)
  for (let replay = 0; replay < 2; replay++) {
    const result = pair.applyAnchorView(session, expanded)
    const wire = allText(result.messages)
    assert.equal(wire.split(literal).length - 1, 1)
    assert.equal(wire.split('FINAL REPORT KEPT NATURALLY').length - 1, 1)
    const retained = result.messages.find((row) => row.info.id === 'current')
    assert.equal(Object.hasOwn(retained.parts[1].state.input, 'carry'), false)
    assert.equal(retained.parts[1].state.input.program, 'return true;')
    assert.equal(retained.parts[1].state.input.prefixUntil, 0)
  }
  assert.deepEqual(expanded, saved)
})

test('WHAT[context-compression-031] same-message appended prose and results remain outside the old final anchor', () => {
  const raw = [user('u', 'question'), assistant('shared', [tool('read-1', {}, 'old result')])]
  pair.applyAnchorView(session, raw)
  declare(0, [])
  const next = structuredClone(raw)
  next[1].parts.push({ type: 'text', text: 'new final report' }, tool('deliver-call', {}, 'delivered'))
  const result = pair.applyAnchorView(session, next)
  assert.doesNotMatch(allText(result.messages), /old result/)
  assert.match(allText(result.messages), /new final report/)
  assert.match(allText(result.messages), /delivered/)
  assert.equal(result.messages[0].parts.length, 2)
  assert.equal(anchors(session).length, 1)
})

test('WHAT[context-compression-031] compression does not strip matching fields from another tool', () => {
  const raw = [user('u', 'old context')]
  pair.applyAnchorView(session, raw)
  declareForCall('current', 'other-call', 0, ['literal'])
  const other = tool('other-call', { prefixUntil: 0, carry: ['literal'] }, 'received')
  other.tool = 'send-payload'
  const result = pair.applyAnchorView(session, [...raw, assistant('current', [other])])
  const kept = result.messages.find((row) => row.info.id === 'current')
  assert.deepEqual(kept.parts[0].state.input, { prefixUntil: 0, carry: ['literal'] })
})

test('WHAT[context-compression-031] only the committing message and call lose carry among matching tools', () => {
  const raw = [user('u', 'old context')]
  pair.applyAnchorView(session, raw)
  declareForCall('shared', 'committed', 0, ['literal'])
  const args = { prefixUntil: 0, carry: ['literal'] }
  const expanded = [
    ...raw,
    assistant('shared', [
      tool('committed', structuredClone(args), 'same output'),
      tool('not-committed', structuredClone(args), 'same output'),
    ]),
    assistant('another-message', [tool('committed', structuredClone(args), 'same output')]),
  ]
  const saved = structuredClone(expanded)
  for (let replay = 0; replay < 2; replay++) {
    const result = pair.applyAnchorView(session, expanded)
    const retained = result.messages.find((row) => row.info.id === 'shared')
    assert.equal(Object.hasOwn(retained.parts[0].state.input, 'carry'), false)
    assert.deepEqual(retained.parts[1].state.input, args)
    const otherMessage = result.messages.find((row) => row.info.id === 'another-message')
    assert.deepEqual(otherMessage.parts[0].state.input, args)
  }
  assert.deepEqual(expanded, saved)
})

test('WHAT[context-compression-031] a declaration without an owning call never strips matching tool arguments', () => {
  const raw = [user('u', 'old context')]
  pair.applyAnchorView(session, raw)
  declare(0, ['literal'])
  const args = { prefixUntil: 0, carry: ['literal'] }
  const current = assistant('current', [tool('unattributed', structuredClone(args), 'ok')])
  const result = pair.applyAnchorView(session, [...raw, current])
  const retained = result.messages.find((row) => row.info.id === 'current')
  assert.deepEqual(retained.parts[0].state.input, args)
  assert.deepEqual(current.parts[0].state.input, args)
})

test('WHAT[context-compression-031] a growing prose part retains only its newly produced text after compression', () => {
  const raw = [user('u', 'question'), assistant('a', [{ type: 'text', text: 'earlier ' }])]
  pair.applyAnchorView(session, raw)
  declare(0, [])
  const next = structuredClone(raw)
  next[1].parts[0].text += 'new report'
  const result = pair.applyAnchorView(session, next)
  assert.match(allText(result.messages), /new report/)
  assert.doesNotMatch(allText(result.messages), /earlier/)
})

test('WHAT[context-compression-031] bilingual guidance explains only the current compression practice', () => {
  for (const language of ['en', 'zh-CN']) {
    const prose = readFileSync(new URL('../../../resources/provider/tool/js-program/view-declaration-extra/' + language + '.md', import.meta.url), 'utf8')
    assert.match(prose, /carry/)
    assert.match(prose, /prefixUntil/)
    assert.doesNotMatch(prose, /thenMap|reportMapping/)
    assert.match(prose, /prefixCount/)
    assert.match(prose, language === 'en' ? /current turn|this turn/ : /本回合|当前回合/)
  }
})

