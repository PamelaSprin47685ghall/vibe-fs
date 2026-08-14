// fetch(shelfmark): exact canonical answer + natural freshness consequence.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { CasebookWorkflow_archiveInspectorResult as archive } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import { contentHash as hash } from '../../../dist/Infrastructure/CasebookCapture.js'
import { shelfmarkFor } from '../../../dist/Infrastructure/CasebookIndex.js'
import { Observation } from '../../../dist/Domain/Casebook.js'
import { GitRawStore_createInMemory as createRaw } from '../../../dist/Infrastructure/Persist/GitRawStore.js'
import { EventStore_create as createStore } from '../../../dist/Infrastructure/Persist/EventStore.js'
import { listItems, toList, resultOf } from '../../verification-system/tests/support/domain.mjs'
import { CANONICAL_A, CANONICAL_Q, scriptedBookkeeperPort } from './bookkeeper-session.test.mjs'
import {
  BookkeeperRuntime_setSessionPort as setSessionPort,
  BookkeeperRuntime_resetSessionPort as resetSessionPort,
} from '../../../dist/Infrastructure/BookkeeperRuntime.js'
import { HostToolArguments_$ctor_4E60E31B as hostArgs } from '../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js'

const obsIndex = (name) => Object.create(Observation.prototype).cases().indexOf(name)
const fileRead = (path, h) => new Observation(obsIndex('FileRead'), [path, h])

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-fetch-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const factoryFor = async () => {
  const codec = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
  return codec.ToolHostCodec_factory({ tool: { schema: { string: () => ({ type: 'string' }) } } })
}

const buildTool = async (dir, store, raw) => {
  const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/FetchTool.js')
  return spec(await factoryFor(), dir, store, raw)
}

const assertNoMachineFreshness = (text) => {
  assert.doesNotMatch(text, /\b(session_id|status|freshness|refresh)\s*=/)
}

test('CASE004_fetch_uses_shelfmark_and_natural_fresh_refreshed_no_case_consequences', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const raw = createRaw()
    const store = createStore(raw)
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = {
      SessionId: 's1',
      Q: 'When does CaseFinalize run?',
      A: 'A1',
      Observations: toList([fileRead('a.txt', hash('hello'))]),
      LastAccessOrder: 0,
    }
    assert.equal(resultOf(await archive(store, raw, caseRec)).ok, true)

    const tool = await buildTool(dir, store, raw)
    assert.equal(tool.Name, 'fetch')
    assert.deepEqual(listItems(tool.Arguments).map(([name]) => name), ['shelfmark'])
    const shelfmark = shelfmarkFor('s1', caseRec.Q)

    const fresh = await tool.Execute(hostArgs({ shelfmark }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.match(fresh, /No change was found in the evidence this answer depended on/)
    assert.match(fresh, /answer = "A1"/)
    assertNoMachineFreshness(fresh)
    assert.equal(fresh.includes('s1'), false)

    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    const { port, createCalls, programCalls } = scriptedBookkeeperPort()
    setSessionPort(port)
    const afterChange = await tool.Execute(hostArgs({ shelfmark }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.match(afterChange, /evidence this case depended on had changed/i)
    assert.equal(afterChange.includes(CANONICAL_A), true, afterChange)
    assert.equal(afterChange.includes('evidence:'), false)
    assertNoMachineFreshness(afterChange)
    assert.equal(createCalls.length, 1, 'exactly one Bookkeeper child on stale fetch')
    assert.equal(programCalls.length >= 1, true)

    const refreshedShelfmark = shelfmarkFor('s1', CANONICAL_Q)
    const again = await tool.Execute(hostArgs({ shelfmark: refreshedShelfmark }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.match(again, /No change was found in the evidence this answer depended on/)
    assert.equal(again.includes(CANONICAL_A), true)
    assertNoMachineFreshness(again)

    const missing = await tool.Execute(hostArgs({ shelfmark: 'Nothing here · 00000000' }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.match(missing, /contains no entry under that shelfmark/i)
    assertNoMachineFreshness(missing)
  } finally {
    resetSessionPort()
    cleanup()
  }
})

test('CASE009_fetch_never_writes_the_subject', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const raw = createRaw()
    const store = createStore(raw)
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = {
      SessionId: 's1',
      Q: 'Q',
      A: 'A',
      Observations: toList([fileRead('a.txt', hash('hello'))]),
      LastAccessOrder: 0,
    }
    resultOf(await archive(store, raw, caseRec))
    const tool = await buildTool(dir, store, raw)
    await tool.Execute(hostArgs({ shelfmark: shelfmarkFor('s1', 'Q') }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'hello')
  } finally {
    cleanup()
  }
})

test('CASE011_fetch_single_flight_serializes_same_shelfmark', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const raw = createRaw()
    const store = createStore(raw)
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = {
      SessionId: 's1',
      Q: 'Q',
      A: 'A',
      Observations: toList([fileRead('a.txt', hash('hello'))]),
      LastAccessOrder: 0,
    }
    resultOf(await archive(store, raw, caseRec))
    const tool = await buildTool(dir, store, raw)
    const shelfmark = shelfmarkFor('s1', 'Q')

    const [a, b] = await Promise.all([
      tool.Execute(hostArgs({ shelfmark }), { sessionID: 'ses', agent: 'fast-inspector' }),
      tool.Execute(hostArgs({ shelfmark }), { sessionID: 'ses', agent: 'fast-inspector' }),
    ])
    assert.match(a, /No change was found/)
    assert.match(a, /answer = "A"/)
    assert.equal(b, a, 'single-flight callers must observe the same result bytes')
    assertNoMachineFreshness(a)

    const later = await tool.Execute(hostArgs({ shelfmark }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.match(later, /No change was found/)
  } finally {
    cleanup()
  }
})
