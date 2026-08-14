// tests/unit/casebook/lifecycle-wiring.test.mjs — G6: CasebookLifecycle
// session wiring (draft Q/A → finalize once; cleanup never publishes).

import assert from 'node:assert/strict'
import test from 'node:test'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  collector,
  setEnabled,
  notePrompt,
  noteAnswer,
  tryFinalizeInspector,
  cleanupInspector,
  touchAccess,
} from '../../../dist/Infrastructure/CasebookLifecycle.js'
import {
  CasebookWorkflow_fetchCase as fetchCase,
  CasebookWorkflow_touchCaseAccess as touchCaseAccess,
} from '../../../dist/Infrastructure/CasebookWorkflow.js'
import {
  ObservationCollector__Collect_Z15AE2BE0 as collect,
  ObservationCollector__Count_Z721C83C5 as count,
} from '../../../dist/Infrastructure/ObservationCollector.js'
import { loadEvents, project } from '../../../dist/Infrastructure/CasebookStore.js'
import { acquire } from '../../../dist/Infrastructure/OpenCode/Host/WorkspaceEventStore.js'
import { gitCommonDir } from '../../../dist/Journal/RuntimePath.js'
import { caseOf, listItems, mapEntries, resultOf } from '../../../tests/unit/support/domain.mjs'
import {
  CANONICAL_A,
  CANONICAL_Q,
  scriptedBookkeeperPort,
} from './bookkeeper-session.test.mjs'
import {
  BookkeeperRuntime_setSessionPort as setSessionPort,
  BookkeeperRuntime_resetSessionPort as resetSessionPort,
} from '../../../dist/Infrastructure/BookkeeperRuntime.js'


const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-lifecycle-'))
  execFileSync('git', ['init', '--quiet', dir])
  mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const envelopeTypes = async (raw, store) => {
  const events = resultOf(await loadEvents(raw, await store.OpenSnapshot()))
  assert.equal(events.ok, true, `loadEvents ok, got ${JSON.stringify(events.error)}`)
  return listItems(events.value).map((e) => caseOf(e))
}

test('lifecycle_notePrompt_noteAnswer_tryFinalize_creates_case_once', async () => {
  const { dir, cleanup } = sandbox()
  try {
    setEnabled(dir)
    const { port } = scriptedBookkeeperPort()
    setSessionPort(port)
    const sessionId = 'insp-finalize-1'
    notePrompt(sessionId, 'What owns PromptAuthority?')
    collect(collector, sessionId, 'read', { path: 'a.txt' }, 'hello')
    const rawA = 'PromptAuthority is owned by the Host.'
    noteAnswer(sessionId, rawA)

    const first = resultOf(await tryFinalizeInspector(dir, sessionId))
    assert.equal(first.ok, true, `first finalize ok, got ${JSON.stringify(first.error)}`)

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    const fetched = resultOf(await fetchCase(store, raw, 10, sessionId))
    assert.equal(fetched.ok, true)
    assert.equal(fetched.value !== undefined && fetched.value !== null, true, 'case present after finalize')
    assert.equal(fetched.value.Q, CANONICAL_Q)
    assert.notEqual(fetched.value.Q, 'What owns PromptAuthority?')
    assert.notEqual(fetched.value.A, rawA, 'finalize synthesizes A (not raw noteAnswer)')
    assert.equal(fetched.value.A, CANONICAL_A)
    assert.equal(fetched.value.A.includes('evidence:'), false)
    assert.equal(listItems(fetched.value.Observations).length, 1)

    const publishedA = fetched.value.A

    // Re-seed and finalize again — finalizeCase refuses the second publication.
    notePrompt(sessionId, 'Q2')
    noteAnswer(sessionId, 'A2')
    const second = resultOf(await tryFinalizeInspector(dir, sessionId))
    assert.equal(second.ok, false, 'second finalize must be refused')
    assert.equal(String(second.error).includes('already finalized'), true)

    const still = resultOf(await fetchCase(store, raw, 10, sessionId))
    assert.equal(still.value.A, publishedA, 'original synthesized case retained')
  } finally {
    resetSessionPort()
    setEnabled(undefined)
    cleanup()
  }
})

test('lifecycle_cleanupInspector_never_writes_eventstore', async () => {
  const { dir, cleanup } = sandbox()
  try {
    setEnabled(dir)
    const sessionId = 'insp-cleanup-1'
    notePrompt(sessionId, 'Q cleanup')
    collect(collector, sessionId, 'read', { path: 'b.txt' }, 'body')
    noteAnswer(sessionId, 'A cleanup')
    assert.equal(count(collector, sessionId) > 0, true)

    cleanupInspector(sessionId)
    assert.equal(count(collector, sessionId), 0, 'collector buffer drained')

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    const types = await envelopeTypes(raw, store)
    assert.equal(types.length, 0, 'cleanup must not append Casebook events')

    const fetched = resultOf(await fetchCase(store, raw, 10, sessionId))
    assert.equal(fetched.ok, true)
    assert.equal(fetched.value === undefined || fetched.value === null, true, 'no case after cleanup')

    // A later finalize after cleanup alone (no re-seed) is a no-op Ok.
    const after = resultOf(await tryFinalizeInspector(dir, sessionId))
    assert.equal(after.ok, true)
    assert.equal((await envelopeTypes(raw, store)).length, 0)
  } finally {
    setEnabled(undefined)
    cleanup()
  }
})

test('lifecycle_missing_answer_is_noop_finalize', async () => {
  const { dir, cleanup } = sandbox()
  try {
    setEnabled(dir)
    const sessionId = 'insp-no-a'
    notePrompt(sessionId, 'Q only')
    const r = resultOf(await tryFinalizeInspector(dir, sessionId))
    assert.equal(r.ok, true)

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    assert.equal((await envelopeTypes(raw, store)).length, 0)
  } finally {
    setEnabled(undefined)
    cleanup()
  }
})

test('lifecycle_touchAccess_and_touchCaseAccess_append_accessed', async () => {
  const { dir, cleanup } = sandbox()
  try {
    setEnabled(dir)
    const { port } = scriptedBookkeeperPort()
    setSessionPort(port)
    const sessionId = 'insp-access-1'
    notePrompt(sessionId, 'Q')
    noteAnswer(sessionId, 'A')
    assert.equal(resultOf(await tryFinalizeInspector(dir, sessionId)).ok, true)

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)

    // Direct workflow helper (in-memory-compatible path).
    const touched = resultOf(await touchCaseAccess(store, raw, sessionId))
    assert.equal(touched.ok, true, `touchCaseAccess ok, got ${JSON.stringify(touched.error)}`)

    // Host-side helper (acquires WorkspaceEventStore for workspace).
    await touchAccess(dir, sessionId)

    const types = await envelopeTypes(raw, store)
    assert.equal(types.includes('CaseCaptured'), true)
    assert.equal(types.filter((t) => t === 'CaseAccessed').length >= 1, true)

    const cases = project(10, resultOf(await loadEvents(raw, await store.OpenSnapshot())).value)
    assert.equal(mapEntries(cases).some(([k]) => k === sessionId), true)
  } finally {
    resetSessionPort()
    setEnabled(undefined)
    cleanup()
  }
})

test('lifecycle_disabled_marker_skips_publication', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-lifecycle-off-'))
  try {
    execFileSync('git', ['init', '--quiet', dir])
    // no .wanxiang/casebook marker
    setEnabled(dir)
    const sessionId = 'insp-off'
    notePrompt(sessionId, 'Q')
    noteAnswer(sessionId, 'A')
    const r = resultOf(await tryFinalizeInspector(dir, sessionId))
    assert.equal(r.ok, true)

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    assert.equal((await envelopeTypes(raw, store)).length, 0)
  } finally {
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})
