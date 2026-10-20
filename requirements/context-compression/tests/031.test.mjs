import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { anchors, applyView, clearAll, countTokens, numberMessage, captureView as setView, takeView } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'

const session = 'view-contract'
const input = (...texts) => texts.map((text, index) => ({ id: String(index + 1), text }))
const number = (rows) => { for (const row of rows) if (row.id) numberMessage(session, row.id); return rows }
const declaration = (prefixUntil, thenMap) => ({ prefixUntil, thenMap })
const declare = (prefixUntil, thenMap) => {
  const result = setView(session, declaration(prefixUntil, thenMap))
  assert.equal(result.ok, true, 'valid declaration must succeed')
}
const render = (rows) => applyView(session, rows).entries.map((entry) => entry.kind === 'kept' ? entry.message.text : entry.summary)
test.beforeEach(() => clearAll())

test('WHAT[context-compression-031] real anchors start at 1, remain stable, and declarations are consumed once', () => {
  assert.equal(numberMessage(session, 'one'), 1)
  assert.equal(numberMessage(session, 'one'), 1)
  declare(1, { 1: null })
  assert.deepEqual(takeView(session), declaration(1, { 1: null }))
  assert.equal(takeView(session), null)
})

test('WHAT[context-compression-031] both fields are required and invalid shapes have zero effects', () => {
  const rows = number(input('one', 'two'))
  const bad = [
    {}, { prefixUntil: 1 }, { thenMap: {} }, { prefixUntil: 'all', thenMap: {} },
    { prefixUntil: -1, thenMap: {} }, { prefixUntil: 1.5, thenMap: {} },
    { prefixUntil: 1, thenMap: null }, { prefixUntil: 1, thenMap: [] },
    { prefixUntil: 1, thenMap: { two: 'summary' } },
    { prefixUntil: 1, thenMap: { 1: '' } }, { prefixUntil: 1, thenMap: { 1: '  ' } },
    { prefixUntil: 1, thenMap: { 1: 7 } },
    { prefixUntil: 1, thenMap: { 1: 'valid', 2: false } },
    { prefixUntil: 1, thenRemove: {} }, { prefixUntil: 1, thenMap: {}, extra: 'old' },
  ]
  for (const invalid of bad) {
    assert.equal(setView(session, invalid).ok, false, JSON.stringify(invalid))
    assert.equal(takeView(session), null)
    assert.deepEqual(render(rows), ['one', 'two'])
  }
})

test('WHAT[context-compression-031] null and omitted keys keep content; prefix is protected, boundary is not', () => {
  const rows = number(input('one', 'two', 'three', 'four'))
  declare(2, { 1: 'must be ignored', 2: 'summary-two', 3: null })
  assert.deepEqual(render(rows), ['one', 'summary-two', 'three', 'four'])
  assert.deepEqual(anchors(session).map((entry) => entry.anchor), [1, 3, 4])
})

test('WHAT[context-compression-031] protection and null never restore previously compressed content', () => {
  const rows = number(input('one', 'two', 'three'))
  declare(1, { 2: 'summary-two' })
  assert.deepEqual(render(rows), ['one', 'summary-two', 'three'])
  declare(99, { 2: 'forbidden update' })
  assert.deepEqual(render(rows), ['one', 'summary-two', 'three'])
  declare(0, { 2: null })
  assert.deepEqual(render(rows), ['one', 'summary-two', 'three'])
  assert.deepEqual(render(rows), ['one', 'summary-two', 'three'])
})

test('WHAT[context-compression-031] replacing 6 removes its anchor; later 5 replaces the entire latest interval', () => {
  const rows = number(input('one', 'two', 'three', 'four', 'five', 'six', 'seven'))
  declare(5, { 5: null, 6: 'new-six', 7: null })
  assert.deepEqual(render(rows), ['one', 'two', 'three', 'four', 'five', 'new-six', 'seven'])
  assert.equal(anchors(session).some((entry) => entry.anchor === 6), false)
  declare(5, { 5: 'five-and-six', 7: null })
  assert.deepEqual(render(rows), ['one', 'two', 'three', 'four', 'five-and-six', 'seven'])
  assert.deepEqual(render(rows), ['one', 'two', 'three', 'four', 'five-and-six', 'seven'])
})

test('WHAT[context-compression-031] adjacent replacements in one map retain both summaries regardless of key order', () => {
  const rows = number(input('one', 'two', 'three'))
  declare(1, { 2: 'two-summary', 1: 'one-summary', 3: null })
  assert.deepEqual(render(rows), ['one-summary', 'two-summary', 'three'])
  declare(0, { 0: 'opening-summary', 3: null })
  assert.deepEqual(render(rows), ['opening-summary', 'three'])
})

test('WHAT[context-compression-031] implicit 0 represents replaced 1 and survives repeated replacement', () => {
  const rows = number(input('one', 'two', 'three'))
  declare(1, { 1: 'summary-one', 2: null, 3: null })
  assert.deepEqual(render(rows), ['summary-one', 'two', 'three'])
  declare(0, { 0: 'opening-one', 2: null, 3: null })
  assert.deepEqual(render(rows), ['opening-one', 'two', 'three'])
  declare(0, { 0: 'opening-two', 2: 'summary-two', 3: null })
  assert.deepEqual(render(rows), ['opening-two', 'summary-two', 'three'])
  declare(0, { 0: 'opening-three', 3: null })
  assert.deepEqual(render(rows), ['opening-three', 'three'])
})

test('WHAT[context-compression-031] a replacement covers unnumbered rows inside its interval, not later new content', () => {
  const rows = number(input('one', 'two'))
  rows.splice(1, 0, { text: 'inside-one' })
  declare(1, { 1: 'whole-first-interval', 2: null })
  assert.deepEqual(render(rows), ['whole-first-interval', 'two'])
  declare(1, { 2: 'whole-last-interval' })
  assert.deepEqual(render(rows), ['whole-first-interval', 'whole-last-interval'])
  const expanded = number([...rows, { id: '3', text: 'new-content' }])
  assert.deepEqual(render(expanded), ['whole-first-interval', 'whole-last-interval', 'new-content'])
})

test('WHAT[context-compression-031] a missing declaration preserves history and token accounting uses rendered content', () => {
  const rows = number(input('one', 'two'))
  assert.deepEqual(render(rows), ['one', 'two'])
  declare(1, { 1: 'short' })
  render(rows)
  declare(3, {})
  const result = applyView(session, rows)
  assert.equal(result.report.used, countTokens('short') + countTokens('two'))
  assert.equal(result.report.cached, result.report.used)
})

test('WHAT[context-compression-031] bilingual prompts teach full coverage, implicit zero and missing-key preservation', () => {
  for (const language of ['en', 'zh-CN']) {
    const text = readFileSync(new URL('../../../resources/provider/tool/js-program/view-declaration-extra/' + language + '.md', import.meta.url), 'utf8')
    assert.match(text, /thenMap/)
    assert.match(text, /null/)
    assert.match(text, /0/)
    assert.doesNotMatch(text, /thenRemove/)
    assert.match(text, language === 'en' ? /every visible/ : /所有.*可见/)
  }
})


test('WHAT[context-compression-031] real Host schema admits nullable maps and rejects missing fields before program execution', async () => {
  const { tool } = await import('@opencode-ai/plugin/tool')
  const { createRegistered, argumentSchema, execute } = await import('../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js')
  const { mkdtempSync, existsSync, rmSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-view-schema-'))
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
    numberMessage(session, 'schema-anchor')
    const valid = await execute(registered, {
      program: 'class Js extends JsProgram { async run() { this.write("ran.txt", "yes"); return true; } }',
      prefixUntil: 1,
      thenMap: { 1: null },
    }, { sessionID: session })
    assert.equal(existsSync(join(dir, 'ran.txt')), true, String(valid))
    const refused = await execute(registered, {
      program: 'class Js extends JsProgram { async run() { this.write("must-not-exist.txt", "no"); return true; } }',
      prefixUntil: 1,
    }, { sessionID: session })
    assert.match(String(refused), /VIEW_DECLARATION_INVALID/)
    assert.equal(existsSync(join(dir, 'must-not-exist.txt')), false)
    const missingPrefix = await execute(registered, {
      program: 'class Js extends JsProgram { async run() { this.write("must-not-exist-2.txt", "no"); return true; } }',
      thenMap: { 1: null },
    }, { sessionID: session })
    assert.match(String(missingPrefix), /VIEW_DECLARATION_INVALID/)
    assert.equal(existsSync(join(dir, 'must-not-exist-2.txt')), false)
  } finally { rmSync(dir, { recursive: true, force: true }) }
})

test('WHAT[context-compression-031] generated Host schema accepts null map values and requires both fields', async () => {
  const { z } = await import('zod')
  const { createRegistered, argumentSchema, execute } = await import('../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js')
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
  const missingMap = await execute(handle, { program, prefixUntil: 0 }, { sessionID: session })
  assert.match(String(missingMap), /VIEW_DECLARATION_INVALID/)
  const missingPrefix = await execute(handle, { program, thenMap: {} }, { sessionID: session })
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


test('WHAT[context-compression-031] inline anchors mark user and tool results only and preserve the request prefix', async () => {
  const pair = await import('../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js')
  const user = { info: { id: 'u', role: 'user' }, parts: [{ type: 'text', text: 'question' }] }
  const prose = { info: { id: 'a', role: 'assistant' }, parts: [{ type: 'reasoning', text: 'thinking' }, { type: 'text', text: 'answer' }] }
  const call = { info: { id: 'c', role: 'assistant' }, parts: [{ type: 'tool', tool: 'read', callID: 'call', state: { status: 'running', input: {} } }] }
  const result = { info: { id: 'r', role: 'assistant' }, parts: [{ type: 'tool', tool: 'read', callID: 'call', state: { status: 'completed', input: {}, output: 'result' } }] }
  const raw = [user, prose, call, result]
  const snapshot = structuredClone(raw)
  const first = pair.applyAnchorView(session, raw)
  assert.equal(first.messages[0].parts[0].text, '[ANCHOR: 1]\0\uFEFFquestion')
  assert.deepEqual(first.messages[1], prose)
  assert.deepEqual(first.messages[2], call)
  assert.equal(first.messages[3].parts[0].state.output, '[ANCHOR: 2]\0\uFEFFresult')
  assert.equal(first.messages[3].parts.length, 1, 'no assistant prose carrier for a tool result')
  assert.deepEqual(anchors(session).map(row => row.anchor), [1, 2])
  assert.deepEqual(raw, snapshot, 'the projection must not mutate source messages')
  assert.equal(first.report, null, 'no changing tail map without a declaration')
  declare(3, {})
  const next = pair.applyAnchorView(session, [...raw, { info: { id: 'u2', role: 'user' }, parts: [{ type: 'text', text: 'next' }] }])
  assert.deepEqual(next.messages.slice(0, raw.length), first.messages)
  assert.doesNotMatch(next.report, /ANCHOR map/)
  declare(2, { 2: 'short result' })
  const compressed = pair.applyAnchorView(session, raw)
  assert.deepEqual(compressed.messages.slice(0, 3), first.messages.slice(0, 3))
  assert.equal(compressed.messages[3].parts[0].text, 'short result')
  assert.deepEqual(anchors(session).map(row => row.anchor), [1, 3])
})
