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

    // content changed → stale with refresh intent
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    const stale = await tool.Execute({ session_id: 's1' }, { sessionID: 'ses', agent: 'fast-inspector' })
    assert.equal(stale.includes('status = "stale"'), true)
    assert.equal(stale.includes('refresh = "required"'), true)

    // unknown session → no-case
    const missing = await tool.Execute({ session_id: 'nope' }, { sessionID: 'ses', agent: 'fast-inspector' })
    assert.equal(missing.includes('status = "no-case"'), true)
  } finally {
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
