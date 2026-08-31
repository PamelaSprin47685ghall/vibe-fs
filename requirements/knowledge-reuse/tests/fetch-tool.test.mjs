// FROZEN — 2026-08-14. Fetch reads canonical Casebook Current only.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'
import * as fetchSurface from '../../../dist/Repository/Knowledge/Casebook/FetchSurface.js'
import * as index from '../../../dist/Repository/Knowledge/Casebook/IndexSurface.js'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import {
  CANONICAL_A,
  CANONICAL_Q,
  installBookkeeperRuntime,
  scriptedBookkeeperPort,
} from './bookkeeper-session.test.mjs'

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })
const sandbox = ({ enabled = true } = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-fetch-'))
  if (enabled) mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  const handle = eventStore.create(dir, 'fetch-tool')
  return { dir, handle, cleanup: () => { eventStore.dispose(handle); rmSync(dir, { recursive: true, force: true }) } }
}
const factory = { tool: { schema: { string: () => ({}) } } }
const buildTool = (dir, handle) => fetchSurface.contract(factory, dir, handle)
const record = (sessionId, q, a, observations) => ({ sessionId, q, a, observations, lastAccessOrder: 0 })
const execute = (tool, shelfmark) => tool.execute({ shelfmark }, { sessionID: 'ses', agent: 'fast-inspector' })
const assertFresh = (text) => assert.match(text, /No change was found in the evidence this answer depended on\.|这份答案所依赖的证据没有变化。/i)
const assertRefreshed = (text) => assert.match(text, /The evidence this case depended on had changed\.|这份 case 所依赖的证据已经变化。/i)
const assertNoCase = (text) => assert.match(text, /The Casebook contains no entry under that shelfmark\.|Casebook 在该 shelfmark 下没有条目。/i)
const assertUnavailable = (text) => assert.match(text, /could not be read from this execution context|无法从当前执行环境读取/i)
const assertNoMachineFreshness = (text) => assert.doesNotMatch(text, /\b(session_id|status|freshness|refresh)\s*=/)

test('WHAT[KNOWLEDGE-REUSE-004] CASE004_fetch_uses_shelfmark_and_replays_before_refreshing', async () => {
  const { dir, handle, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = record('s1', 'When does CaseFinalize run?', 'A1', [fileRead('a.txt', casebook.contentHash('hello'))])
    assert.equal((await casebook.archive(handle, caseRec)).ok, true)
    const tool = buildTool(dir, handle)
    assert.equal(tool.name, 'fetch')
    const shelfmark = index.shelfmarkFor('s1', caseRec.q)

    const fresh = await execute(tool, shelfmark)
    assertFresh(fresh)
    assertNoMachineFreshness(fresh)
    assert.equal(fresh.includes('s1'), false)

    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    const { port, createCalls, programCalls } = scriptedBookkeeperPort()
    await installBookkeeperRuntime(port, ['s1'])
    const afterChange = await execute(tool, shelfmark)
    assertRefreshed(afterChange)

    assert.equal(afterChange.includes(CANONICAL_A), true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)

    const again = await execute(tool, index.shelfmarkFor('s1', CANONICAL_Q))
    assertFresh(again)
    const missing = await execute(tool, 'Nothing here · 00000000')
    assertNoCase(missing)

  } finally {
    bookkeeper.resetRuntime()
    cleanup()
  }
})

test('WHAT[KNOWLEDGE-REUSE-002] CASE004_fetch_returns_exact_canonical_a', async () => {
  const { dir, handle, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = record('s1', 'When does CaseFinalize run?', 'A1', [fileRead('a.txt', casebook.contentHash('hello'))])
    assert.equal((await casebook.archive(handle, caseRec)).ok, true)
    const tool = buildTool(dir, handle)
    const shelfmark = index.shelfmarkFor('s1', caseRec.q)

    const fresh = await execute(tool, shelfmark)
    assert.match(fresh, /answer = "A1"/)
  } finally {
    cleanup()
  }
})

test('WHAT[KNOWLEDGE-REUSE-009] CASE009_fetch_execution_rejects_a_workspace_without_the_marker', async () => {
  const { dir, handle, cleanup } = sandbox({ enabled: false })
  try {
    const tool = buildTool(dir, handle)
    const result = await execute(tool, 'Anything · 00000000')

    assertUnavailable(result)
    assert.equal((await casebook.fetchCase(handle, 10, 'ses')).value, null)
  } finally {
    cleanup()
  }
})

test('WHAT[KNOWLEDGE-REUSE-004] CASE009_fetch_never_writes_the_subject', async () => {
  const { dir, handle, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    await casebook.archive(handle, record('s1', 'Q', 'A', [fileRead('a.txt', casebook.contentHash('hello'))]))
    const tool = buildTool(dir, handle)
    await execute(tool, index.shelfmarkFor('s1', 'Q'))
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'hello')
  } finally {
    cleanup()
  }
})

test('WHAT[KNOWLEDGE-REUSE-011] CASE011_fetch_single_flight_serializes_same_shelfmark', async () => {
  const { dir, handle, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    await casebook.archive(handle, record('s1', 'Q', 'A', [fileRead('a.txt', casebook.contentHash('hello'))]))
    const tool = buildTool(dir, handle)
    const shelfmark = index.shelfmarkFor('s1', 'Q')
    const [a, b] = await Promise.all([execute(tool, shelfmark), execute(tool, shelfmark)])
    assertFresh(a)
    assert.equal(b, a)
  } finally {
    cleanup()
  }
})
