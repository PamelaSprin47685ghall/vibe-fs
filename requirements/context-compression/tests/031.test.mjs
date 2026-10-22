import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  anchors,
  applyView,
  clearAll,
  clearSession,
  countTokens,
  numberMessage,
  captureView as setView,
  takeView,
} from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'

const session = () => `sess-${Math.random().toString(36).slice(2, 10)}`
const declaration = (prefixUntil, thenMap) => ({ prefixUntil, thenMap })

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
  assert.equal(one, 1)
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
    { prefixUntil: 1, thenMap: { 1: '  ' } },
    { prefixUntil: 1, thenMap: { 1: 'valid', 2: false } },
    { prefixUntil: 1, thenMap: {}, extra: 'old' },
  ]) {
    assert.equal(setView(id, malformed).ok, false, JSON.stringify(malformed))
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

test('WHAT[context-compression-031] omitted intervals stay dropped while null and the protected prefix remain', () => {
  const id = session()
  const messages = messagesOf(id, [
    { text: 'opening' },
    { id: 'a', text: 'alpha' },
    { text: 'alpha reasoning' },
    { id: 'b', text: 'beta' },
    { id: 'c', text: 'gamma' },
  ])
  setView(id, { prefixUntil: 2, thenMap: { 2: null } })
  assert.deepEqual(rendered(applyView(id, messages)), ['opening', 'alpha', 'alpha reasoning', 'beta'])
  assert.deepEqual(anchors(id), [
    { anchor: 1, messageId: 'a' },
    { anchor: 2, messageId: 'b' },
  ])
  assert.deepEqual(rendered(applyView(id, messages)), ['opening', 'alpha', 'alpha reasoning', 'beta'],
    'consuming the declaration does not restore the omitted interval')
  setView(id, { prefixUntil: 0, thenMap: { 0: null, 1: null, 2: null, 3: null } })
  assert.deepEqual(rendered(applyView(id, messages)), ['opening', 'alpha', 'alpha reasoning', 'beta'],
    'a later null cannot restore an anchor that no longer exists')
})

test('WHAT[context-compression-031] an empty map drops every unprotected interval', () => {
  const id = session()
  const messages = messagesOf(id, [{ text: 'opening' }, { id: 'a', text: 'alpha' }, { id: 'b', text: 'beta' }])
  setView(id, { prefixUntil: 0, thenMap: {} })
  assert.deepEqual(rendered(applyView(id, messages)), [])
  assert.deepEqual(anchors(id), [])
  assert.deepEqual(rendered(applyView(id, messages)), [], 'the empty selection persists after declaration consumption')
})

test('WHAT[context-compression-031] a summary stands in for its anchor for the rest of the session', () => {
  const id = session()
  const messages = messagesOf(id, [
    { id: 'a', text: 'alpha' },
    { id: 'b', text: 'beta' },
    { id: 'c', text: 'gamma' },
  ])

  setView(id, { prefixUntil: 1, thenMap: { 1: null, 2: 'b held the tool dump', 3: null } })
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

  setView(id, { prefixUntil: 1, thenMap: { 1: 'a held the brief', 2: null, 3: null } })
  applyView(id, messages)
  setView(id, { prefixUntil: 1, thenMap: { 2: null, 3: 'c held the tail' } })
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

  setView(id, { prefixUntil: 1, thenMap: { 1: null, 2: 'b held the tool dump', 3: null } })
  applyView(id, messages)

  setView(id, { prefixUntil: 1, thenMap: { 1: 'a and the tool dump, in one paragraph', 3: null } })
  const result = applyView(id, messages)
  assert.deepEqual(
    rendered(result),
    ['#a and the tool dump, in one paragraph', 'gamma'],
    'anchor 2 stopped existing, so replacing anchor 1 folded its summary away',
  )
  assert.equal(result.report.folded, 1)
  assert.deepEqual(result.report.replaced.sort(), [1, 2], 'both anchors are gone: one summarized, one folded')
})

test('WHAT[context-compression-031] unidentified content remains when its interval is explicitly kept', () => {
  const id = session()
  const messages = messagesOf(id, [
    { id: 'a', text: 'alpha' },
    { text: 'unidentified tail' },
  ])
  setView(id, { prefixUntil: 1, thenMap: { 1: null, 9: 'nothing matches anchor 9' } })
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
  setView(id, { prefixUntil: 0, thenMap: { 0: null, 1: 'summary alpha', 2: null } })
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
  setView(id, { prefixUntil: 0, thenMap: { 0: null, 2: 'summary beta', 1: 'summary alpha', 3: null } })
  assert.deepEqual(rendered(applyView(id, messages)), ['#summary alpha', '#summary beta', 'gamma'])
})

test('WHAT[context-compression-031] anchor zero persists and earlier replacement includes prior summaries', () => {
  const id = session()
  const messages = messagesOf(id, [
    { text: 'opening' }, { id: 'a', text: 'alpha' },
    { id: 'b', text: 'beta' }, { id: 'c', text: 'gamma' }, { id: 'd', text: 'delta' },
  ])
  setView(id, { prefixUntil: 0, thenMap: { 0: null, 1: null, 2: null, 3: 'summary gamma', 4: null } })
  applyView(id, messages)
  setView(id, { prefixUntil: 0, thenMap: { 0: null, 1: null, 2: 'beta and gamma', 4: null } })
  assert.deepEqual(rendered(applyView(id, messages)), ['opening', 'alpha', '#beta and gamma', 'delta'])
  setView(id, { prefixUntil: 0, thenMap: { 0: 'summary opening', 1: null, 4: null } })
  assert.deepEqual(rendered(applyView(id, messages)), ['#summary opening', 'alpha', '#beta and gamma', 'delta'])
  setView(id, { prefixUntil: 0, thenMap: { 0: 'new opening', 1: null, 4: null } })
  assert.deepEqual(rendered(applyView(id, messages)), ['#new opening', 'alpha', '#beta and gamma', 'delta'])
})

test('WHAT[context-compression-031] a captured tail interval cannot swallow a later message', () => {
  const id = session()
  const messages = messagesOf(id, [{ id: 'a', text: 'alpha' }, { id: 'b', text: 'beta' }])
  setView(id, { prefixUntil: 0, thenMap: { 0: null, 1: null, 2: 'summary beta' } })
  applyView(id, messages)
  const later = [...messages, { id: 'c', text: 'later gamma' }]
  numberMessage(id, 'c')
  assert.deepEqual(rendered(applyView(id, later)), ['alpha', '#summary beta', 'later gamma'])
})

test('WHAT[context-compression-031] a generated tool captures the view argument and refuses a malformed one', async () => {
  const { createRegistered, argumentSchema, execute } = await import(
    '../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js'
  )
  const { tool } = await import('@opencode-ai/plugin/tool')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-anchor-view-'))
  const sessionId = session()
  try {
    const registered = createRegistered({ tool }, 'Engineer', 'en', dir, null, null)
    assert.ok(registered)
    // The declaration is two flat top-level arguments; there is no wrapper.
    const prefixSchema = argumentSchema(registered, 'prefixUntil')
    const mapSchema = argumentSchema(registered, 'thenMap')
    assert.ok(prefixSchema, 'prefixUntil is a top-level argument')
    assert.ok(mapSchema, 'thenMap is a top-level argument')
    assert.equal(prefixSchema.safeParse(1).success, true)
    assert.equal(prefixSchema.safeParse('all').success, false)
    assert.equal(mapSchema.safeParse({ 1: null, 2: 'summary' }).success, true)
    assert.equal(mapSchema.safeParse({ 1: 7 }).success, false)
    assert.equal(mapSchema.safeParse([]).success, false)
    numberMessage(sessionId, 'schema-anchor')
    const valid = await execute(registered, {
      program: 'class Js extends JsProgram { async run() { this.write("ran.txt", "yes"); return true; } }',
      prefixUntil: 1,
      thenMap: { 1: null },
    }, { sessionID: sessionId })
    assert.equal(existsSync(join(dir, 'ran.txt')), true, String(valid))
    assert.deepEqual(takeView(sessionId), { prefixUntil: 1, thenMap: { 1: null } })
    const refused = await execute(registered, {
      program: 'class Js extends JsProgram { async run() { this.write("must-not-exist.txt", "no"); return true; } }',
      prefixUntil: 1,
    }, { sessionID: sessionId })
    assert.match(String(refused), /VIEW_DECLARATION_INVALID/)
    assert.equal(existsSync(join(dir, 'must-not-exist.txt')), false)
    const missingPrefix = await execute(registered, {
      program: 'class Js extends JsProgram { async run() { this.write("must-not-exist-2.txt", "no"); return true; } }',
      thenMap: { 1: null },
    }, { sessionID: sessionId })
    assert.match(String(missingPrefix), /VIEW_DECLARATION_INVALID/)
    assert.equal(existsSync(join(dir, 'must-not-exist-2.txt')), false)
  } finally { rmSync(dir, { recursive: true, force: true }) }
})

test('WHAT[context-compression-031] generated Host schema accepts null map values and requires both fields', async () => {
  const { z } = await import('zod')
  const { createRegistered, argumentSchema, execute } = await import('../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js')
  const sessionId = session()
  const tool = definition => definition
  tool.schema = z
  const handle = createRegistered({ tool }, 'Engineer', 'en', process.cwd(), null, null)
  const prefixSchema = argumentSchema(handle, 'prefixUntil')
  const mapSchema = argumentSchema(handle, 'thenMap')
  assert.equal(prefixSchema.safeParse(0).success, true)
  assert.equal(prefixSchema.safeParse('0').success, false)
  assert.equal(mapSchema.safeParse({ 0: null, 1: 'summary' }).success, true)
  assert.equal(mapSchema.safeParse({}).success, true)
  for (const bad of [{ 1: 7 }, null, []]) {
    assert.equal(mapSchema.safeParse(bad).success, false, JSON.stringify(bad))
  }
  // Both flat fields are required: either half alone is refused.
  const program = 'class Js extends JsProgram { async run() { return true; } }'
  const missingMap = await execute(handle, { program, prefixUntil: 0 }, { sessionID: sessionId })
  assert.match(String(missingMap), /VIEW_DECLARATION_INVALID/)
  const missingPrefix = await execute(handle, { program, thenMap: {} }, { sessionID: sessionId })
  assert.match(String(missingPrefix), /VIEW_DECLARATION_INVALID/)
})

test('WHAT[context-compression-031] a failed program commits no view declaration and keeps the previous one', async () => {
  const { tool } = await import('@opencode-ai/plugin/tool')
  const { createRegistered, execute } = await import('../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js')
  const { mkdtempSync, rmSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-view-failure-'))
  const failing = 'class Js extends JsProgram { async run() { throw new Error("boom"); } }'
  const succeeding = 'class Js extends JsProgram { async run() { return true; } }'
  try {
    const registered = createRegistered({ tool }, 'Engineer', 'en', dir, null, null)
    // A failed call on its own leaves the pending view empty.
    const alone = 'view-contract-failure-alone'
    const failed = await execute(registered, { program: failing, prefixUntil: 1, thenMap: { 1: null } }, { sessionID: alone })
    assert.match(String(failed), /PROGRAM_FAILED/)
    assert.equal(takeView(alone), null, 'a failed call leaves no presentable declaration')
    // A failed call does not overwrite the declaration a successful call committed.
    const kept = 'view-contract-failure-kept'
    const ok = await execute(registered, { program: succeeding, prefixUntil: 1, thenMap: { 1: null } }, { sessionID: kept })
    assert.doesNotMatch(String(ok), /PROGRAM_FAILED/)
    const failedAgain = await execute(registered, { program: failing, prefixUntil: 2, thenMap: { 2: 'later-summary' } }, { sessionID: kept })
    assert.match(String(failedAgain), /PROGRAM_FAILED/)
    assert.deepEqual(takeView(kept), declaration(1, { 1: null }), 'the failed call must not overwrite the committed declaration')
    assert.equal(takeView(kept), null)
  } finally { rmSync(dir, { recursive: true, force: true }) }
})
