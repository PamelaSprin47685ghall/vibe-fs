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
} from './support/bookkeeper-session-support.mjs'

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })

const sandbox = ({ enabled = true } = {}) => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-fetch-'))
  if (enabled) mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  const handle = eventStore.create(dir, 'fetch-tool')
  return { dir, handle, cleanup: () => { eventStore.dispose(handle); rmSync(dir, { recursive: true, force: true }) } }
}

const factory = { tool: { schema: { string: () => ({}) } } }

const record = (sessionId, q, a, observations) => ({ sessionId, q, a, observations, lastAccessOrder: 0 })

const execute = (tool, shelfmark) => tool.execute({ shelfmark }, { sessionID: 'ses', agent: 'inspector' })

const assertFresh = (text) => assert.match(text, /No change was found in the evidence this answer depended on\.|这份答案所依赖的证据没有变化。/i)

const assertRefreshed = (text) => assert.match(text, /The evidence this case depended on had changed\.|这份 case 所依赖的证据已经变化。/i)

const assertNoCase = (text) => assert.match(text, /The Casebook contains no entry under that shelfmark\.|Casebook 在该 shelfmark 下没有条目。/i)

const assertUnavailable = (text) => assert.match(text, /could not be read from this execution context|无法从当前执行环境读取|当前执行上下文无法读取/i)

const assertNoMachineFreshness = (text) => assert.doesNotMatch(text, /\b(session_id|status|freshness|refresh)\s*=/)

test('WHAT[KNOWLEDGE-REUSE-011] CASE011_fetch_single_flight_serializes_same_shelfmark', async () => {
  const { dir, handle, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    await casebook.archive(handle, record('s1', 'Q', 'A', [fileRead('a.txt', casebook.contentHash('hello'))]))
    const tool = fetchSurface.contract(factory, dir, handle)
    const shelfmark = index.shelfmarkFor('s1', 'Q')
    const [a, b] = await Promise.all([execute(tool, shelfmark), execute(tool, shelfmark)])
    assertFresh(a)
    assert.equal(b, a)
  } finally {
    cleanup()
  }
})
