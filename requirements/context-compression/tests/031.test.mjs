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
  setView(id, { prefixUntil: 3, extra: [1, 'carry this prose'] })
  assert.deepEqual(takeView(id), { prefixUntil: 3, extra: [1, 'carry this prose'] })
  assert.equal(takeView(id), null, 'the declaration does not persist')
})

test('WHAT[context-compression-031] applyView keeps the prefix, drops the tail and re-adds referenced anchors', () => {
  const id = session()
  const messages = [
    { id: 'a', text: 'alpha' },
    { id: 'b', text: 'beta' },
    { id: 'c', text: 'gamma' },
  ]
  const a = numberMessage(id, 'a')
  const c = numberMessage(id, 'c')
  numberMessage(id, 'b')
  setView(id, { prefixUntil: a, extra: [c, 'carried prose'] })

  const result = applyView(id, messages)
  assert.deepEqual(result.messages.map((message) => message.id), ['a', 'c'], 'prefix plus the referenced anchor')
  assert.deepEqual(result.prose, ['carried prose'])
  assert.equal(result.report.kept, 2)
  assert.equal(result.report.dropped, 1)
  assert.ok(result.report.total > 0)
  assert.ok(result.report.cached <= result.report.total, 'the cached prefix is a subset of the total')
  assert.ok(result.report.used <= result.report.total + countTokens('carried prose'))
})

test('WHAT[context-compression-031] messages without an id are never silently dropped', () => {
  const id = session()
  const messages = [
    { id: 'a', text: 'alpha' },
    { text: 'unidentified tail' },
  ]
  const a = numberMessage(id, 'a')
  setView(id, { prefixUntil: a, extra: [] })
  const result = applyView(id, messages)
  assert.deepEqual(result.messages.map((message) => message.text), ['alpha', 'unidentified tail'])
})

test('WHAT[context-compression-031] without a declaration applyView leaves the message list unchanged', () => {
  const id = session()
  const messages = [
    { id: 'a', text: 'alpha' },
    { id: 'b', text: 'beta' },
  ]
  const result = applyView(id, messages)
  assert.deepEqual(result.messages.map((message) => message.id), ['a', 'b'])
  assert.equal(result.prose.length, 0)
  assert.equal(result.report.dropped, 0)
})

test('WHAT[context-compression-031] countTokens reports the o200k_base token count', () => {
  assert.ok(countTokens('hello world') > 0)
  assert.ok(countTokens('hello world') >= countTokens('hello'))
})

test('WHAT[context-compression-031] a generated tool captures the view argument for the next request', async () => {
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
        view: { prefixUntil: 2, extra: [1, 'keep this'] },
      },
      { sessionID: sessionId },
    )
    assert.deepEqual(takeView(sessionId), { prefixUntil: 2, extra: [1, 'keep this'] })
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
