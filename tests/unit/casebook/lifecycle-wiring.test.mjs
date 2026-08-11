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
import { caseOf, listItems, mapEntries, resultOf } from '../support/domain.mjs'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-lifecycle-'))
  execFileSync('git', ['init', '--quiet', dir])
  mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const envelopeTypes = (raw, store) => {
  const events = resultOf(loadEvents(raw, store.OpenSnapshot()))
  assert.equal(events.ok, true, `loadEvents ok, got ${JSON.stringify(events.error)}`)
  return listItems(events.value).map((e) => caseOf(e))
}

test('lifecycle_notePrompt_noteAnswer_tryFinalize_creates_case_once', () => {
  const { dir, cleanup } = sandbox()
  try {
    setEnabled(dir)
    const sessionId = 'insp-finalize-1'
    notePrompt(sessionId, 'What owns PromptAuthority?')
    collect(collector, sessionId, 'read', { path: 'a.txt' }, 'hello')
    noteAnswer(sessionId, 'PromptAuthority is owned by the Host.')

    const first = resultOf(tryFinalizeInspector(dir, sessionId))
    assert.equal(first.ok, true, `first finalize ok, got ${JSON.stringify(first.error)}`)

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    const fetched = resultOf(fetchCase(store, raw, 10, sessionId))
    assert.equal(fetched.ok, true)
    assert.equal(fetched.value !== undefined && fetched.value !== null, true, 'case present after finalize')
    assert.equal(fetched.value.Q, 'What owns PromptAuthority?')
    assert.equal(fetched.value.A, 'PromptAuthority is owned by the Host.')
    assert.equal(listItems(fetched.value.Observations).length, 1)

    // Re-seed and finalize again — finalizeCase refuses the second publication.
    notePrompt(sessionId, 'Q2')
    noteAnswer(sessionId, 'A2')
    const second = resultOf(tryFinalizeInspector(dir, sessionId))
    assert.equal(second.ok, false, 'second finalize must be refused')
    assert.equal(String(second.error).includes('already finalized'), true)

    const still = resultOf(fetchCase(store, raw, 10, sessionId))
    assert.equal(still.value.A, 'PromptAuthority is owned by the Host.', 'original case retained')
  } finally {
    setEnabled(undefined)
    cleanup()
  }
})

test('lifecycle_cleanupInspector_never_writes_eventstore', () => {
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
    const types = envelopeTypes(raw, store)
    assert.equal(types.length, 0, 'cleanup must not append Casebook events')

    const fetched = resultOf(fetchCase(store, raw, 10, sessionId))
    assert.equal(fetched.ok, true)
    assert.equal(fetched.value === undefined || fetched.value === null, true, 'no case after cleanup')

    // A later finalize after cleanup alone (no re-seed) is a no-op Ok.
    const after = resultOf(tryFinalizeInspector(dir, sessionId))
    assert.equal(after.ok, true)
    assert.equal(envelopeTypes(raw, store).length, 0)
  } finally {
    setEnabled(undefined)
    cleanup()
  }
})

test('lifecycle_missing_answer_is_noop_finalize', () => {
  const { dir, cleanup } = sandbox()
  try {
    setEnabled(dir)
    const sessionId = 'insp-no-a'
    notePrompt(sessionId, 'Q only')
    const r = resultOf(tryFinalizeInspector(dir, sessionId))
    assert.equal(r.ok, true)

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    assert.equal(envelopeTypes(raw, store).length, 0)
  } finally {
    setEnabled(undefined)
    cleanup()
  }
})

test('lifecycle_touchAccess_and_touchCaseAccess_append_accessed', () => {
  const { dir, cleanup } = sandbox()
  try {
    setEnabled(dir)
    const sessionId = 'insp-access-1'
    notePrompt(sessionId, 'Q')
    noteAnswer(sessionId, 'A')
    assert.equal(resultOf(tryFinalizeInspector(dir, sessionId)).ok, true)

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)

    // Direct workflow helper (in-memory-compatible path).
    const touched = resultOf(touchCaseAccess(store, raw, sessionId))
    assert.equal(touched.ok, true, `touchCaseAccess ok, got ${JSON.stringify(touched.error)}`)

    // Host-side helper (acquires WorkspaceEventStore for workspace).
    touchAccess(dir, sessionId)

    const types = envelopeTypes(raw, store)
    assert.equal(types.includes('CaseCaptured'), true)
    assert.equal(types.filter((t) => t === 'CaseAccessed').length >= 1, true)

    const cases = project(10, resultOf(loadEvents(raw, store.OpenSnapshot())).value)
    assert.equal(mapEntries(cases).some(([k]) => k === sessionId), true)
  } finally {
    setEnabled(undefined)
    cleanup()
  }
})

test('lifecycle_disabled_marker_skips_publication', () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-lifecycle-off-'))
  try {
    execFileSync('git', ['init', '--quiet', dir])
    // no .wanxiang/casebook marker
    setEnabled(dir)
    const sessionId = 'insp-off'
    notePrompt(sessionId, 'Q')
    noteAnswer(sessionId, 'A')
    const r = resultOf(tryFinalizeInspector(dir, sessionId))
    assert.equal(r.ok, true)

    const common = gitCommonDir(dir)
    const [raw, store] = acquire(common)
    assert.equal(envelopeTypes(raw, store).length, 0)
  } finally {
    setEnabled(undefined)
    rmSync(dir, { recursive: true, force: true })
  }
})
