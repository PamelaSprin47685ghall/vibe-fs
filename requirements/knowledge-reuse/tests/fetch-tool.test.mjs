// FROZEN — 2026-08-14. Fetch reads canonical Casebook Current only.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { CasebookWorkflow_archiveInspectorResult as archive } from '../../../dist/Repository/Knowledge/Casebook/Workflow.js'
import { contentHash as hash } from '../../../dist/Repository/Knowledge/Casebook/Capture.js'
import { shelfmarkFor } from '../../../dist/Repository/Knowledge/Casebook/Index.js'
import { Observation } from '../../../dist/Repository/Knowledge/Casebook/Model.js'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'
import { listItems, toList, resultOf } from '../../verification-system/tests/support/domain.mjs'
import { CANONICAL_A, CANONICAL_Q, scriptedBookkeeperPort } from './bookkeeper-session.test.mjs'
import { BookkeeperRuntime_setSessionPort as setSessionPort, BookkeeperRuntime_resetSessionPort as resetSessionPort } from '../../../dist/Repository/Knowledge/Casebook/BookkeeperRuntime.js'
import { HostToolArguments_$ctor_4E60E31B as hostArgs } from '../../../dist/OpenCode/Codec/ToolHostCodec.js'

const obsIndex = (name) => Object.create(Observation.prototype).cases().indexOf(name)
const fileRead = (path, h) => new Observation(obsIndex('FileRead'), [path, h])
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
const record = (session, q, a, obs) => ({ SessionId: session, Q: q, A: a, Observations: toList(obs), LastAccessOrder: 0 })
const assertNoMachineFreshness = (text) => assert.doesNotMatch(text, /\b(session_id|status|freshness|refresh)\s*=/)

test('CASE004_fetch_uses_shelfmark_and_natural_fresh_refreshed_no_case_consequences', async () => {
  const { dir, cleanup } = sandbox()
  const local = createLocalEventStore()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = record('s1', 'When does CaseFinalize run?', 'A1', [fileRead('a.txt', hash('hello'))])
    assert.equal(resultOf(await archive(local.store, caseRec)).ok, true)
    const tool = await buildTool(dir, local.store)
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
    assert.equal(afterChange.includes(CANONICAL_A), true)
    assert.equal(createCalls.length, 1)
    assert.equal(programCalls.length >= 1, true)

    const again = await tool.Execute(hostArgs({ shelfmark: shelfmarkFor('s1', CANONICAL_Q) }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.match(again, /No change was found/)
    const missing = await tool.Execute(hostArgs({ shelfmark: 'Nothing here · 00000000' }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.match(missing, /contains no entry under that shelfmark/i)
  } finally {
    resetSessionPort()
    local.close()
    cleanup()
  }
})

test('CASE009_fetch_never_writes_the_subject', async () => {
  const { dir, cleanup } = sandbox()
  const local = createLocalEventStore()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    resultOf(await archive(local.store, record('s1', 'Q', 'A', [fileRead('a.txt', hash('hello'))])))
    const tool = await buildTool(dir, local.store)
    await tool.Execute(hostArgs({ shelfmark: shelfmarkFor('s1', 'Q') }), { sessionID: 'ses', agent: 'fast-inspector' })
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'hello')
  } finally {
    local.close()
    cleanup()
  }
})

test('CASE011_fetch_single_flight_serializes_same_shelfmark', async () => {
  const { dir, cleanup } = sandbox()
  const local = createLocalEventStore()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    resultOf(await archive(local.store, record('s1', 'Q', 'A', [fileRead('a.txt', hash('hello'))])))
    const tool = await buildTool(dir, local.store)
    const shelfmark = shelfmarkFor('s1', 'Q')
    const [a, b] = await Promise.all([
      tool.Execute(hostArgs({ shelfmark }), { sessionID: 'ses', agent: 'fast-inspector' }),
      tool.Execute(hostArgs({ shelfmark }), { sessionID: 'ses', agent: 'fast-inspector' }),
    ])
    assert.match(a, /No change was found/)
    assert.equal(b, a)
  } finally {
    local.close()
    cleanup()
  }
})
