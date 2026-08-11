// tests/unit/casebook/fetch-tool.test.mjs — G6-D: the conditional fetch tool
// (CASE-004/009).
//
// Fetch replays stored observations against the current worktree: no-delta →
// 'fresh' with the exact old A; delta → 'stale' with refresh intent. It
// never writes the subject.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { CasebookWorkflow_archiveInspectorResult as archive } from '../../../dist/Infrastructure/CasebookWorkflow.js'
import { contentHash as hash } from '../../../dist/Infrastructure/CasebookCapture.js'
import { Observation } from '../../../dist/Domain/Casebook.js'
import { GitRawStore_createInMemory as createRaw } from '../../../dist/Infrastructure/Persist/GitRawStore.js'
import { EventStore_create as createStore } from '../../../dist/Infrastructure/Persist/EventStore.js'
import { toList, resultOf } from '../support/domain.mjs'
import {
  CANONICAL_A,
  scriptedBookkeeperPort,
} from './bookkeeper-session.test.mjs'
import {
  BookkeeperRuntime_setSessionPort as setSessionPort,
  BookkeeperRuntime_resetSessionPort as resetSessionPort,
} from '../../../dist/Infrastructure/BookkeeperRuntime.js'

const obsIndex = (name) => Object.create(Observation.prototype).cases().indexOf(name)
const fileRead = (path, h) => new Observation(obsIndex('FileRead'), [path, h])

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-fetch-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const buildTool = async (factory) => {
  const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/FetchTool.js')
  return spec
}

test('CASE004_fetch_fresh_and_stale_paths', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const raw = createRaw()
    const store = createStore(raw)
    // archive a Case whose observation matches the current worktree
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = { SessionId: 's1', Q: 'Q1', A: 'A1', Observations: toList([fileRead('a.txt', hash('hello'))]), LastAccessOrder: 0 }
    const archived = resultOf(archive(store, raw, caseRec))
    assert.equal(archived.ok, true)

    const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/FetchTool.js')
    const codec = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
    const toolModule = { tool: { schema: { string: () => ({ type: 'string' }) } } }
    const factory = codec.ToolHostCodec_factory(toolModule)
    const tool = spec(factory, dir, store, raw)
    assert.equal(tool.Name, 'fetch')

    // no-delta → fresh with exact A
    const fresh = await tool.Execute({ session_id: 's1' }, { sessionID: 'ses', agent: 'fast-inspector' })
    assert.equal(fresh.includes('status = "fresh"'), true)
    assert.equal(fresh.includes('A1'), true)

    // content changed → Bookkeeper child + edit-qa revises A, then fresh
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    const { port, createCalls, editQaCalls } = scriptedBookkeeperPort()
    setSessionPort(port)
    const afterChange = await tool.Execute({ session_id: 's1' }, { sessionID: 'ses', agent: 'fast-inspector' })
    assert.equal(afterChange.includes('status = "fresh"'), true, `edit-qa refresh should re-stabilize: ${afterChange}`)
    assert.equal(afterChange.includes(CANONICAL_A), true, afterChange)
    assert.equal(afterChange.includes('evidence:'), false)
    assert.equal(createCalls.length, 1, 'exactly one Bookkeeper child on stale fetch')
    assert.equal(editQaCalls.length >= 2, true)

    // second fetch on stable worktree still fresh
    const again = await tool.Execute({ session_id: 's1' }, { sessionID: 'ses', agent: 'fast-inspector' })
    assert.equal(again.includes('status = "fresh"'), true)

    // unknown session → no-case
    const missing = await tool.Execute({ session_id: 'nope' }, { sessionID: 'ses', agent: 'fast-inspector' })
    assert.equal(missing.includes('status = "no-case"'), true)
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
    const caseRec = { SessionId: 's1', Q: 'Q', A: 'A', Observations: toList([fileRead('a.txt', hash('hello'))]), LastAccessOrder: 0 }
    resultOf(archive(store, raw, caseRec))
    const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/FetchTool.js')
    const codec = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
    const factory = codec.ToolHostCodec_factory({ tool: { schema: { string: () => ({ type: 'string' }) } } })
    const tool = spec(factory, dir, store, raw)
    await tool.Execute({ session_id: 's1' }, { sessionID: 'ses', agent: 'fast-inspector' })
    // worktree untouched — no new files, content unchanged
    assert.equal(readFileSync(join(dir, 'a.txt'), 'utf8'), 'hello')
  } finally {
    cleanup()
  }
})

test('CASE011_fetch_single_flight_serializes_same_session', async () => {
  const { dir, cleanup } = sandbox()
  try {
    const raw = createRaw()
    const store = createStore(raw)
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    const caseRec = { SessionId: 's1', Q: 'Q', A: 'A', Observations: toList([fileRead('a.txt', hash('hello'))]), LastAccessOrder: 0 }
    resultOf(archive(store, raw, caseRec))
    const { spec } = await import('../../../dist/Infrastructure/OpenCode/Tools/FetchTool.js')
    const codec = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')
    const factory = codec.ToolHostCodec_factory({ tool: { schema: { string: () => ({ type: 'string' }) } } })
    const tool = spec(factory, dir, store, raw)

    // two concurrent fetches for the same session_id — both must succeed and
    // agree; the single-flight gate must not corrupt either result
    const [a, b] = await Promise.all([
      tool.Execute({ session_id: 's1' }, { sessionID: 'ses', agent: 'fast-inspector' }),
      tool.Execute({ session_id: 's1' }, { sessionID: 'ses', agent: 'fast-inspector' }),
    ])
    assert.equal(a.includes('status = "fresh"'), true)
    assert.equal(a.includes('a = "A"'), true)
    assert.equal(b, a, 'single-flight callers must observe the same result bytes')

    // after completion the gate is clear — a later fetch still works
    const later = await tool.Execute({ session_id: 's1' }, { sessionID: 'ses', agent: 'fast-inspector' })
    assert.equal(later.includes('status = "fresh"'), true)
  } finally {
    cleanup()
  }
})
