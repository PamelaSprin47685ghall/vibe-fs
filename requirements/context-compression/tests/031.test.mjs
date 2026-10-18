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
  for (const message of messages) numberMessage(id, message.id)
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
  const declaration = { prefixUntil: 3, thenRemove: { 7: 'segment 7 held the benchmark table' } }
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
    { thenRemove: { 1: 'summary' } },
    { prefixUntil: 1, thenRemove: [1] },
    { prefixUntil: 1, thenRemove: { 2: '' } },
    { prefixUntil: 1, thenRemove: { two: 'summary' } },
    { prefixUntil: 1, thenRemove: { 2: 3 } },
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
  setView(id, { prefixUntil: 99 })
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

  setView(id, { prefixUntil: 1, thenRemove: { 2: 'b held the tool dump' } })
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

  setView(id, { prefixUntil: 1, thenRemove: { 1: 'a held the brief' } })
  applyView(id, messages)
  setView(id, { prefixUntil: 1, thenRemove: { 3: 'c held the tail' } })
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

  setView(id, { prefixUntil: 1, thenRemove: { 2: 'b held the tool dump' } })
  applyView(id, messages)

  setView(id, { prefixUntil: 1, thenRemove: { 1: 'a and the tool dump, in one paragraph' } })
  const result = applyView(id, messages)
  assert.deepEqual(
    rendered(result),
    ['#a and the tool dump, in one paragraph', 'gamma'],
    'anchor 2 stopped existing, so replacing anchor 1 folded its summary away',
  )
  assert.equal(result.report.folded, 1)
  assert.deepEqual(result.report.replaced.sort(), [1, 2], 'both anchors are gone: one summarized, one folded')
})

test('WHAT[context-compression-031] messages without an id are never silently dropped', () => {
  const id = session()
  const messages = messagesOf(id, [
    { id: 'a', text: 'alpha' },
    { text: 'unidentified tail' },
  ])
  setView(id, { prefixUntil: 1, thenRemove: { 9: 'nothing matches anchor 9' } })
  const result = applyView(id, messages)
  assert.deepEqual(rendered(result), ['alpha', 'unidentified tail'])
})

test('WHAT[context-compression-031] countTokens reports the o200k_base token count', () => {
  assert.ok(countTokens('hello world') > 0)
  assert.ok(countTokens('hello world') >= countTokens('hello'))
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
        string: () => ({ type: 'string', describe: (text) => ({ type: 'string', description: text }) }),
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
          describe: (description) => ({ type: 'record', key, value, description }),
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
        view: { prefixUntil: 2, thenRemove: { 1: 'keep this' } },
      },
      { sessionID: sessionId },
    )
    assert.deepEqual(takeView(sessionId), { prefixUntil: 2, thenRemove: { 1: 'keep this' } })

    const refused = await execute(
      registered,
      {
        program: 'class Js extends JsProgram { async run() { return { ok: true }; } }',
        view: { prefixUntil: 'all' },
      },
      { sessionID: sessionId },
    )
    assert.match(String(refused), /VIEW_DECLARATION_INVALID/, 'a malformed view fails the call with a stable code')
    assert.equal(takeView(sessionId), null, 'the refused call captured nothing')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
