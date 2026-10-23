import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
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

test('WHAT[context-compression-031] markers expose only the anchor number and raw source remains unchanged', () => {
  const raw = [
    user('u', 'question'),
    assistant('a', [{ type: 'text', text: 'answer' }]),
    assistant('r', [tool('read-1', {}, 'result')]),
  ]
  const saved = structuredClone(raw)
  const first = pair.applyAnchorView(session, raw)
  assert.equal(first.messages[0].parts[0].text, '[ANCHOR: 1]\0\uFEFFquestion')
  assert.deepEqual(first.messages[1], raw[1])
  assert.equal(first.messages[2].parts[0].state.output, '[ANCHOR: 2]\0\uFEFFresult')
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
    '[ANCHOR: 2]\0\uFEFFtwo',
    '[ANCHOR: 1]\0\uFEFFone',
    '[ANCHOR: 2]\0\uFEFFtwo',
  ])
})

test('WHAT[context-compression-031] repeated carry addresses survive durable HOST-013 replay without duplicating guidance', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wanxiang-carry-address-'))
  const opened = await pair.createJournal(directory)
  assert.equal(opened.ok, true, opened.error)
  try {
    const id = 'msg_167f66a46001l05Pw2Nb0UkjUs'
    const raw = [user(id, 'evidence')]
    const initial = await pair.tryInjectWithJournal(opened.journal, session, 'historical hint', raw)
    assert.equal(initial.ok, true, initial.error)
    pair.applyAnchorView(session, raw)
    declare(2, [1, 1])
    const view = pair.applyAnchorView(session, [...raw, user('current', 'new request')]).messages
    assert.deepEqual(view.map(row => row.info.id), [id, id, id, 'current'])
    const saved = structuredClone(view)
    const inject = messages => pair.tryInjectWithJournal(opened.journal, session, 'current hint', messages)
    const result = await inject(view)
    assert.equal(result.ok, true, result.error)
    const expected = structuredClone(view)
    expected[2].parts[0].text += '\0\uFEFF<system>\nhistorical hint\n</system>'
    expected[3].parts[0].text += '\0\uFEFF<system>\ncurrent hint\n</system>'
    assert.deepEqual(result.value, expected)
    assert.equal(pair.pairCount(opened.journal, session), 2)
    assert.deepEqual((await inject(result.value)).value, expected)
    assert.deepEqual((await inject(view)).value, expected)
    assert.equal(pair.pairCount(opened.journal, session), 2)
    assert.deepEqual(view, saved)
  } finally {
    pair.disposeJournal(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[context-compression-031] fast-check: carry multiplicity, reordering and boundary growth preserve every row through HOST-013', async () => {
  const historyRow = fc.record({
    kind: fc.constantFrom('user', 'completed', 'error'),
    body: fc.string({ maxLength: 40 }),
  })
  const scenario = fc.array(historyRow, { minLength: 1, maxLength: 8 }).chain(rows =>
    fc.record({
      rows: fc.constant(rows),
      cutoff: fc.integer({ min: 0, max: rows.length + 1 }),
      repeated: fc.integer({ min: 0, max: rows.length - 1 }),
      references: fc.array(fc.integer({ min: 0, max: rows.length - 1 }), { maxLength: 12 }),
      growth: fc.boolean(),
    }))
  let run = 0
  await fc.assert(fc.asyncProperty(scenario, async ({ rows, cutoff, repeated, references, growth }) => {
    clearAll()
    const sid = `carry-address-property-${++run}`
    const raw = rows.map(({ kind, body }, index) => kind === 'user'
      ? user(`host-${index}`, body)
      : assistant(`host-${index}`, [{
        type: 'tool', tool: 'read', callID: `read-${index}`,
        state: kind === 'error'
          ? { status: 'error', input: {}, error: body }
          : { status: 'completed', input: {}, output: body },
      }]))
    const saved = structuredClone(raw)
    const inject = messages => pair.tryInject(sid, 'property hint', messages)
    const initial = await inject(raw)
    assert.equal(initial.ok, true, initial.error)
    pair.applyAnchorView(sid, raw)
    const carry = [repeated, ...references, repeated]
    assert.equal(captureView(sid, declaration(cutoff, carry.map(index => index + 1))).ok, true)
    const expanded = structuredClone(raw)
    if (growth) expanded.at(-1).parts.push({ type: 'text', text: 'new boundary prose' })
    expanded.push(user('current', 'next request'))
    const view = pair.applyAnchorView(sid, expanded).messages
    const expectedIds = [
      ...raw.slice(0, Math.max(0, cutoff - 1)).map(row => row.info.id),
      ...carry.map(index => raw[index].info.id),
      ...(growth ? [raw.at(-1).info.id] : []),
      'current',
    ]
    assert.deepEqual(view.map(row => row.info.id), expectedIds)
    const expected = structuredClone(view)
    const historicalIndex = expected.findLastIndex(row =>
      row.info.id === raw.at(-1).info.id &&
      (row.info.role === 'user' || row.parts.some(part => part.type === 'tool')))
    if (historicalIndex >= 0) {
      const row = expected[historicalIndex]
      const terminal = row.parts.find(part => part.type === 'tool')
      if (terminal) {
        const field = terminal.state.status === 'error' ? 'error' : 'output'
        terminal.state[field] += '\0\uFEFFproperty hint'
      } else {
        row.parts.at(-1).text += '\0\uFEFF<system>\nproperty hint\n</system>'
      }
    }
    expected.at(-1).parts[0].text += '\0\uFEFF<system>\nproperty hint\n</system>'
    const result = await inject(view)
    assert.equal(result.ok, true, result.error)
    assert.deepEqual(result.value, expected)
    const replay = await inject(result.value)
    assert.equal(replay.ok, true, replay.error)
    assert.deepEqual(replay.value, expected)
    assert.deepEqual(raw, saved)
  }), { seed: 0x484f5313, numRuns: 1000 })
})

test('WHAT[context-compression-031] allowing repeated carry rows does not allow an unaddressed transcript row', async () => {
  const result = await pair.tryInject('carry-missing-address', 'hint', [
    user('addressed', 'original'),
    user('addressed', 'copy'),
    { info: { role: 'user' }, parts: [{ type: 'text', text: 'no address' }] },
    user('current', 'next'),
  ])
  assert.equal(result.ok, false)
  assert.equal(result.error, 'transcript message without address (HOST-013)')
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
  assert.match(projected.messages[2].parts[0].text, new RegExp(`ANCHOR: ${currentAnchor}`))
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
    assert.doesNotMatch(prose, /prefixCount/)
    assert.match(prose, language === 'en' ? /current turn|this turn/ : /本回合|当前回合/)
  }
})

