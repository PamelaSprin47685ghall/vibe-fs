import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  anchors,
  applyView,
  clearAll,
  clearSession,
  countTokens,
  numberMessage,
  setView,
  takeView,
} from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'

const session = () => `sess-${Math.random().toString(36).slice(2, 10)}`

const kinds = (result) => result.entries.map((entry) => entry.kind)

const rendered = (result) =>
  result.entries.map((entry) => (entry.kind === 'kept' ? entry.message.text : `#${entry.summary}`))

const messagesOf = (id, messages) => {
  for (const message of messages) {
    if (typeof message.id === 'string') numberMessage(id, message.id)
  }
  return messages
}

test.beforeEach(() => clearAll())

test('WHAT[context-compression-031] anchor numbers are stable per session and message id', () => {
  const first = session()
  const one = numberMessage(first, 'user-1')
  assert.equal(numberMessage(first, 'user-1'), one, 'the same message id keeps its number')
  const two = numberMessage(first, 'tool-1')
  assert.equal(two > one, true, 'a fresh message id gets the next number')
  assert.deepEqual(
    anchors(first),
    [
      { anchor: one, messageId: 'user-1' },
      { anchor: two, messageId: 'tool-1' },
    ],
    'the map lists anchors ascending',
  )
  assert.deepEqual(anchors(session()), [], 'another session has its own map')
})

test('WHAT[context-compression-031] a view declaration is consumed exactly once', () => {
  const id = session()
  const declaration = { prefixUntil: 3, thenMap: { 7: 'segment 7 held the benchmark table' } }
  setView(id, declaration)
  assert.deepEqual(takeView(id), declaration, 'the declaration echoes back as declared')
  assert.equal(takeView(id), null, 'the declaration does not persist')
})

test('WHAT[context-compression-031] a malformed view declaration is refused and changes nothing', () => {
  const id = session()
  const messages = messagesOf(id, [
    { id: 'a', text: 'alpha' },
    { id: 'b', text: 'beta' },
  ])

  for (const malformed of [
    {},
    { prefixUntil: 'all' },
    { thenMap: { 1: 'summary' } },
    { prefixUntil: 1 },
    { prefixUntil: -1, thenMap: {} },
    { prefixUntil: 1.5, thenMap: {} },
    { prefixUntil: Infinity, thenMap: {} },
    { prefixUntil: null, thenMap: {} },
    { prefixUntil: 1, thenMap: [1] },
    { prefixUntil: 1, thenMap: { 2: '' } },
    { prefixUntil: 1, thenMap: { two: 'summary' } },
    { prefixUntil: 1, thenMap: { 2: 3 } },
    { prefixUntil: 1, thenRemove: {} },
  ]) {
    setView(id, malformed)
    assert.equal(takeView(id), null, `refused declaration must not be captured: ${JSON.stringify(malformed)}`)
  }

  const result = applyView(id, messages)
  assert.deepEqual(kinds(result), ['kept', 'kept'], 'refused declarations leave the request untouched')
})

test('WHAT[context-compression-031] without a declaration applyView leaves the message list unchanged', () => {
  const id = session()
  const messages = messagesOf(id, [
    { id: 'a', text: 'alpha' },
    { id: 'b', text: 'beta' },
  ])
  const result = applyView(id, messages)
  assert.deepEqual(kinds(result), ['kept', 'kept'])
  assert.deepEqual(rendered(result), ['alpha', 'beta'])
  assert.equal(result.report.summarized, 0)
  assert.equal(result.report.folded, 0)
})

test('WHAT[context-compression-031] prefixUntil protects a prefix without replacing anything', () => {
  const id = session()
  const messages = messagesOf(id, [
    { id: 'a', text: 'alpha' },
    { id: 'b', text: 'beta' },
    { id: 'c', text: 'gamma' },
  ])
  setView(id, { prefixUntil: 99, thenMap: {} })
  const result = applyView(id, messages)
  assert.deepEqual(rendered(result), ['alpha', 'beta', 'gamma'], 'nothing is dropped by prefixUntil alone')
  assert.equal(
    result.report.cached,
    countTokens('alpha') + countTokens('beta') + countTokens('gamma'),
    'every anchor below prefixUntil sits in the cache prefix',
  )
  assert.ok(result.report.used <= result.report.total, 'the request is never larger than what it started from')
})

test('WHAT[context-compression-031] a summary stands in for its anchor for the rest of the session', () => {
  const id = session()
  const messages = messagesOf(id, [
    { id: 'a', text: 'alpha' },
    { id: 'b', text: 'beta' },
    { id: 'c', text: 'gamma' },
  ])

  setView(id, { prefixUntil: 1, thenMap: { 2: 'b held the tool dump' } })
  const first = applyView(id, messages)
  assert.deepEqual(kinds(first), ['kept', 'summarized', 'kept'])
  assert.deepEqual(rendered(first), ['alpha', '#b held the tool dump', 'gamma'])
  assert.equal(first.report.summarized, 1)
  assert.equal(first.report.folded, 0)

  const second = applyView(id, messages)
  assert.deepEqual(rendered(second), ['alpha', '#b held the tool dump', 'gamma'], 'the replacement outlives its declaration')

  clearSession(id)
  const third = applyView(id, messages)
  assert.deepEqual(rendered(third), ['alpha', 'beta', 'gamma'], 'a restarted session forgets its replacements')
})

test('WHAT[context-compression-031] separate replacements each stand in place', () => {
  const id = session()
  const messages = messagesOf(id, [
    { id: 'a', text: 'alpha' },
    { id: 'b', text: 'beta' },
    { id: 'c', text: 'gamma' },
  ])

  setView(id, { prefixUntil: 1, thenMap: { 1: 'a held the brief' } })
  applyView(id, messages)
  setView(id, { prefixUntil: 1, thenMap: { 3: 'c held the tail' } })
  const result = applyView(id, messages)
  assert.deepEqual(rendered(result), ['#a held the brief', 'beta', '#c held the tail'])
  assert.equal(result.report.folded, 0)
})

test('WHAT[context-compression-031] replacing an earlier anchor folds the replacements inside its span', () => {
  const id = session()
  const messages = messagesOf(id, [
    { id: 'a', text: 'alpha' },
    { id: 'b', text: 'beta' },
    { id: 'c', text: 'gamma' },
  ])

  setView(id, { prefixUntil: 1, thenMap: { 2: 'b held the tool dump' } })
  applyView(id, messages)

  setView(id, { prefixUntil: 1, thenMap: { 1: 'a and the tool dump, in one paragraph' } })
  const result = applyView(id, messages)
  assert.deepEqual(
    rendered(result),
    ['#a and the tool dump, in one paragraph', 'gamma'],
    'anchor 2 stopped existing, so replacing anchor 1 folded its summary away',
  )
  assert.equal(result.report.folded, 1)
  assert.deepEqual(result.report.replaced.sort(), [1, 2], 'both anchors are gone: one summarized, one folded')
})

test('WHAT[context-compression-031] unidentified content remains when no interval replaces it', () => {
  const id = session()
  const messages = messagesOf(id, [
    { id: 'a', text: 'alpha' },
    { text: 'unidentified tail' },
  ])
  setView(id, { prefixUntil: 1, thenMap: { 9: 'nothing matches anchor 9' } })
  const result = applyView(id, messages)
  assert.deepEqual(rendered(result), ['alpha', 'unidentified tail'])
})

test('WHAT[context-compression-031] countTokens reports the o200k_base token count', () => {
  assert.ok(countTokens('hello world') > 0)
  assert.ok(countTokens('hello world') >= countTokens('hello'))
})

test('WHAT[context-compression-031] registered Host inline anchors are stable projections and do not alter physical originals', async () => {
  const { withGuidanceHost } = await import('../../prefix-stability/tests/support/guidance-host.mjs')
  await withGuidanceHost('completed', async host => {
    const raw = JSON.stringify(host.raw)
    const user = host.first.find(message => message.info.id === host.raw[0].info.id)
    const tool = host.first.find(message => message.info.id === host.result.info.id)
    const known = anchors(host.sessionID)
    const markerFor = id => '[ANCHOR: ' + known.find(item => item.messageId === id).anchor + ']\0\uFEFF'
    assert.ok(user.parts[0].text.startsWith(markerFor(host.raw[0].info.id) + host.raw[0].parts[0].text))
    assert.ok(tool.parts[0].state.output.startsWith(markerFor(host.result.info.id)))
    assert.deepEqual(await host.transform(host.raw), host.first)
    assert.equal(JSON.stringify(host.raw), raw)
    const original = await host.current()
    assert.deepEqual(host.cold().projection, original)
    assert.ok(original.messages.flatMap(message => message.parts)
      .some(part => part.kind === 'text' && part.text === host.raw[0].parts[0].text))
    assert.ok(original.messages.flatMap(message => message.parts)
      .filter(part => part.kind === 'tool-result').every(part => !part.result.includes('[ANCHOR:')))
  }, 'inline-anchors-031', { role: 'engineer', openWorkBeforeTool: true })
})

test('WHAT[context-compression-031] protected items and null mappings keep their current material', () => {
  const id = session()
  const messages = messagesOf(id, [
    { id: 'a', text: 'alpha' }, { id: 'b', text: 'beta' }, { id: 'c', text: 'gamma' },
  ])
  setView(id, { prefixUntil: 2, thenMap: { 1: 'protected', 2: null, 3: 'summary gamma' } })
  assert.deepEqual(rendered(applyView(id, messages)), ['alpha', 'beta', '#summary gamma'])
  setView(id, { prefixUntil: 0, thenMap: { 1: 'summary alpha' } })
  assert.deepEqual(rendered(applyView(id, messages)), ['#summary alpha', 'beta', '#summary gamma'])
  setView(id, { prefixUntil: 99, thenMap: {} })
  const result = applyView(id, messages)
  assert.deepEqual(rendered(result), ['#summary alpha', 'beta', '#summary gamma'])
  assert.equal(result.report.cached, result.report.used)
})

test('WHAT[context-compression-031] same-round adjacent summaries use the input partition', () => {
  const id = session()
  const messages = messagesOf(id, [
    { id: 'a', text: 'alpha' }, { text: 'alpha reasoning' },
    { id: 'b', text: 'beta' }, { id: 'c', text: 'gamma' },
  ])
  setView(id, { prefixUntil: 0, thenMap: { 2: 'summary beta', 1: 'summary alpha' } })
  assert.deepEqual(rendered(applyView(id, messages)), ['#summary alpha', '#summary beta', 'gamma'])
})

test('WHAT[context-compression-031] anchor zero persists and earlier replacement includes prior summaries', () => {
  const id = session()
  const messages = messagesOf(id, [
    { text: 'opening' }, { id: 'a', text: 'alpha' },
    { id: 'b', text: 'beta' }, { id: 'c', text: 'gamma' }, { id: 'd', text: 'delta' },
  ])
  setView(id, { prefixUntil: 0, thenMap: { 3: 'summary gamma' } })
  applyView(id, messages)
  setView(id, { prefixUntil: 0, thenMap: { 2: 'beta and gamma' } })
  assert.deepEqual(rendered(applyView(id, messages)), ['opening', 'alpha', '#beta and gamma', 'delta'])
  setView(id, { prefixUntil: 0, thenMap: { 0: 'summary opening' } })
  assert.deepEqual(rendered(applyView(id, messages)), ['#summary opening', 'alpha', '#beta and gamma', 'delta'])
  setView(id, { prefixUntil: 0, thenMap: { 0: 'new opening' } })
  assert.deepEqual(rendered(applyView(id, messages)), ['#new opening', 'alpha', '#beta and gamma', 'delta'])
})

test('WHAT[context-compression-031] a captured tail interval cannot swallow a later message', () => {
  const id = session()
  const messages = messagesOf(id, [{ id: 'a', text: 'alpha' }, { id: 'b', text: 'beta' }])
  setView(id, { prefixUntil: 0, thenMap: { 2: 'summary beta' } })
  applyView(id, messages)
  const later = [...messages, { id: 'c', text: 'later gamma' }]
  numberMessage(id, 'c')
  assert.deepEqual(rendered(applyView(id, later)), ['alpha', '#summary beta', 'later gamma'])
})

test('WHAT[context-compression-031] a generated tool captures the view argument and refuses a malformed one', async () => {
  const { createRegistered, execute } = await import(
    '../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js'
  )
  const dir = mkdtempSync(join(tmpdir(), 'wxs-anchor-view-'))
  try {
    const toolModule = () => {
      const tool = (definition) => definition
      const any = () => ({ type: 'any', optional: () => ({ type: 'any', optional: true }) })
      tool.schema = {
        string: () => ({
          type: 'string',
          nullable: () => ({ type: 'string', nullable: true }),
          describe: (text) => ({ type: 'string', description: text }),
        }),
        number: () => ({ type: 'number', optional: () => ({ type: 'number', optional: true }) }),
        any,
        array: (item) => ({
          type: 'array',
          item,
          describe: (description) => ({ type: 'array', item, description }),
        }),
        record: (key, value) => ({
          type: 'record',
          key,
          value,
          describe: (description) => ({ type: 'record', key, value, description,
            optional: () => ({ type: 'record', key, value, description, optional: true }) }),
        }),
        object: (shape) => ({ type: 'object', shape }),
      }
      return { tool }
    }
    const registered = createRegistered(toolModule(), 'Coder', 'en', dir, null)
    const sessionId = session()

    await execute(
      registered,
      {
        program: 'class Js extends JsProgram { async run() { return { ok: true }; } }',
        prefixUntil: 2, thenMap: { 1: 'keep this' },
      },
      { sessionID: sessionId },
    )
    assert.deepEqual(takeView(sessionId), { prefixUntil: 2, thenMap: { 1: 'keep this' } })

    const refused = await execute(
      registered,
      {
        program: 'class Js extends JsProgram { async run() { return { ok: true }; } }',
        prefixUntil: 'all', thenMap: {},
      },
      { sessionID: sessionId },
    )
    assert.match(String(refused), /VIEW_DECLARATION_INVALID/, 'a malformed view fails the call with a stable code')
    assert.equal(takeView(sessionId), null, 'the refused call captured nothing')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
