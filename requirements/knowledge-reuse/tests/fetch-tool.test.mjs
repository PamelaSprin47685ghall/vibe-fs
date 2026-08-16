// FROZEN — 2026-08-14. Fetch reads canonical Casebook Current only.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import { contentHash as hash } from '../../../dist/Repository/Knowledge/Casebook/Capture.js'
import { shelfmarkFor } from '../../../dist/Repository/Knowledge/Casebook/Index.js'
import { listItems } from '../../verification-system/tests/support/domain.mjs'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'
import { CANONICAL_A, CANONICAL_Q, scriptedBookkeeperPort } from './bookkeeper-session.test.mjs'
import { BookkeeperRuntime_setSessionPort as setSessionPort, BookkeeperRuntime_resetSessionPort as resetSessionPort } from '../../../dist/Repository/Knowledge/Casebook/BookkeeperRuntime.js'
import { HostToolArguments_$ctor_4E60E31B as hostArgs } from '../../../dist/OpenCode/Codec/ToolHostCodec.js'

const fileRead = (path, h) => ({ kind: 'file-read', path, contentHash: h })
const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-fetch-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const factoryFor = async () => {
  const codec = await import('../../../dist/OpenCode/Codec/ToolHostCodec.js')
  return codec.ToolHostCodec_factory({ tool: { schema: { string: () => ({ type: 'string' }) } } })
}
const buildTool = async (dir, store) => {
  const { spec } = await import('../../../dist/OpenCode/Tools/FetchTool.js')
  return spec(await factoryFor(), dir, store)
}
const record = (session, q, a, obs) => ({ sessionId: session, q, a, observations: obs, lastAccessOrder: 0 })
const assertNoMachineFreshness = (text) => assert.doesNotMatch(text, /\b(session_id|status|freshness|refresh)\s*=/)

test('WHAT[KNOWLEDGE-REUSE-004] CASE004_fetch_uses_shelfmark_and_replays_before_refreshing', async () => {
  const { dir, cleanup } = sandbox()
  const local = createLocalEventStore()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = record('s1', 'When does CaseFinalize run?', 'A1', [fileRead('a.txt', hash('hello'))])
    assert.equal((await casebook.archive(local.store, caseRec)).ok, true)
    const tool = await buildTool(dir, local.store)
    assert.equal(tool.Name, 'fetch')
    assert.deepEqual(listItems(tool.Arguments).map(([name]) => name), ['shelfmark'])
    const shelfmark = shelfmarkFor('s1', caseRec.q)

    const fresh = await tool.Execute(hostArgs({ shelfmark }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.match(fresh, /没有变化/)
    assertNoMachineFreshness(fresh)
    assert.equal(fresh.includes('s1'), false)

    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    const { port, createCalls, programCalls } = scriptedBookkeeperPort()
    setSessionPort(port)
    const afterChange = await tool.Execute(hostArgs({ shelfmark }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.match(afterChange, /已经变化/)
    assert.equal(afterChange.includes(CANONICAL_A), true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)

    const again = await tool.Execute(hostArgs({ shelfmark: shelfmarkFor('s1', CANONICAL_Q) }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.match(again, /没有变化/)
    const missing = await tool.Execute(hostArgs({ shelfmark: 'Nothing here · 00000000' }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.match(missing, /没有条目/)
  } finally {
    resetSessionPort()
    local.close()
    cleanup()
  }
})

test('WHAT[KNOWLEDGE-REUSE-002] CASE004_fetch_returns_exact_canonical_a', async () => {
  const { dir, cleanup } = sandbox()
  const local = createLocalEventStore()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = record('s1', 'When does CaseFinalize run?', 'A1', [fileRead('a.txt', hash('hello'))])
    const archived = await casebook.archive(local.store, caseRec)
    assert.equal(archived.ok, true)
    const tool = await buildTool(dir, local.store)
    const shelfmark = shelfmarkFor('s1', caseRec.q)

    const fresh = await tool.Execute(hostArgs({ shelfmark }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.match(fresh, /answer = "A1"/)
  } finally {
    local.close()
    cleanup()
  }
})

test('WHAT[KNOWLEDGE-REUSE-001] CASE009_fetch_never_writes_the_subject', async () => {
  const { dir, cleanup } = sandbox()
  const local = createLocalEventStore()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    await casebook.archive(local.store, record('s1', 'Q', 'A', [fileRead('a.txt', hash('hello'))]))
    const tool = await buildTool(dir, local.store)
    await tool.Execute(hostArgs({ shelfmark: shelfmarkFor('s1', 'Q') }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'hello')
  } finally {
    local.close()
    cleanup()
  }
})

test('WHAT[KNOWLEDGE-REUSE-011] CASE011_fetch_single_flight_serializes_same_shelfmark', async () => {
  const { dir, cleanup } = sandbox()
  const local = createLocalEventStore()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    await casebook.archive(local.store, record('s1', 'Q', 'A', [fileRead('a.txt', hash('hello'))]))
    const tool = await buildTool(dir, local.store)
    const shelfmark = shelfmarkFor('s1', 'Q')
    const [a, b] = await Promise.all([
      tool.Execute(hostArgs({ shelfmark }), { sessionID: 'ses', agent: 'fast-inspector' }),
      tool.Execute(hostArgs({ shelfmark }), { sessionID: 'ses', agent: 'fast-inspector' }),
    ])
    assert.match(a, /没有变化/)
    assert.equal(b, a)
  } finally {
    local.close()
    cleanup()
  }
})
