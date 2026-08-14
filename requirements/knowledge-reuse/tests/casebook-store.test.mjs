// FROZEN — 2026-08-14. Shock-cut to canonical Integrator Current.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, mkdirSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import {
  appendCaptured,
  appendRefreshed,
  appendAccessed,
  appendEvicted,
} from '../../../dist/Infrastructure/CasebookStore.js'
import { Observation } from '../../../dist/Domain/Casebook.js'
import { createLocalEventStore } from '../../verification-system/tests/support/local-event-store.mjs'
import { caseOf, listItems, mapEntries, resultOf, toList } from '../../verification-system/tests/support/domain.mjs'

const obsIndex = (name) => Object.create(Observation.prototype).cases().indexOf(name)
const fileRead = (path, hash) => new Observation(obsIndex('FileRead'), [path, hash])
const globResult = (pattern, paths) => new Observation(obsIndex('GlobResult'), [pattern, toList(paths)])

const unwrap = async (result) => {
  const r = resultOf(await result)
  assert.equal(r.ok, true, `expected Ok, got ${JSON.stringify(r.error)}`)
  return r.value
}

const caseRec = (sessionId, q, a, observations) => ({
  SessionId: sessionId,
  Q: q,
  A: a,
  Observations: toList(observations),
  LastAccessOrder: 0,
})

const casesOf = (store) => {
  const current = store.TryCurrent('Casebook')
  return current?.Cases ?? new Map()
}

const findCase = (store, sessionId) => mapEntries(casesOf(store)).find(([key]) => key === sessionId)?.[1]

test('CASE007_captured_refreshed_round_trip_through_integrator_Current', async () => {
  const local = createLocalEventStore()
  try {
    await unwrap(appendCaptured(local.store, caseRec('s1', 'Q1', 'A1', [fileRead('a.txt', 'h1')])))
    await unwrap(appendRefreshed(local.store, 's1', 'Q1b', 'A1b', toList([fileRead('a.txt', 'h1'), globResult('*.fs', ['x'])])))
    const s1 = findCase(local.store, 's1')
    assert.equal(s1.A, 'A1b')
    assert.equal(listItems(s1.Observations).length, 2)
  } finally {
    local.close()
  }
})

test('CASE007_accessed_and_evicted_are_integrated_without_feature_history_scan', async () => {
  const local = createLocalEventStore()
  try {
    await unwrap(appendCaptured(local.store, caseRec('s1', 'Q', 'A', [])))
    const before = findCase(local.store, 's1').LastAccessOrder
    await unwrap(appendAccessed(local.store, 's1'))
    assert.ok(findCase(local.store, 's1').LastAccessOrder >= before)
    await unwrap(appendEvicted(local.store, 's1'))
    assert.equal(findCase(local.store, 's1'), undefined)
  } finally {
    local.close()
  }
})

test('CASE007_store_has_no_loadEvents_project_or_history_reader', async () => {
  const { readFileSync } = await import('node:fs')
  const source = readFileSync(new URL('../../../src/Wanxiangshu/Infrastructure/CasebookStore.fs', import.meta.url), 'utf8')
  assert.doesNotMatch(source, /loadEvents|loadEnvelopes|project\s*\(|OpenSnapshot|readStreams/)
  assert.match(source, /tryDecodeEnvelope/)
})

test('CASE009_marker_gates_the_surface', async () => {
  const { CasebookFeature_isEnabled: isEnabled } = await import('../../../dist/Infrastructure/CasebookWorkflow.js')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-cbmarker-'))
  try {
    assert.equal(isEnabled(dir), false)
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    assert.equal(isEnabled(dir), true)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('CASE004_005_workflow_archive_fetch_freshness_reads_Current_only', async () => {
  const {
    CasebookWorkflow_archiveInspectorResult: archive,
    CasebookWorkflow_fetchCase: fetchCase,
    CasebookWorkflow_checkFreshness: checkFreshness,
  } = await import('../../../dist/Infrastructure/CasebookWorkflow.js')
  const local = createLocalEventStore()
  try {
    assert.equal(resultOf(await archive(local.store, caseRec('s1', 'Q1', 'A1', [fileRead('a.txt', 'h1')]))).ok, true)
    const fetched = resultOf(await fetchCase(local.store, 10, 's1'))
    assert.equal(fetched.ok, true)
    assert.equal(fetched.value.A, 'A1')
    assert.equal(caseOf(checkFreshness(fetched.value, toList([fileRead('a.txt', 'h1')]))), 'Fresh')
    assert.equal(caseOf(checkFreshness(fetched.value, toList([fileRead('a.txt', 'h2')]))), 'Stale')
  } finally {
    local.close()
  }
})

test('CASE006_refresh_and_needsRefresh_use_the_same_Current', async () => {
  const {
    CasebookWorkflow_archiveInspectorResult: archive,
    CasebookWorkflow_fetchCase: fetchCase,
    CasebookWorkflow_refreshCase: refreshCase,
    CasebookWorkflow_needsRefresh: needsRefresh,
  } = await import('../../../dist/Infrastructure/CasebookWorkflow.js')
  const { contentHash: hash } = await import('../../../dist/Infrastructure/CasebookCapture.js')
  const dir = mkdtempSync(join(tmpdir(), 'wxs-cbrefresh-'))
  const local = createLocalEventStore()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    resultOf(await archive(local.store, caseRec('s1', 'Q1', 'A1', [fileRead('a.txt', hash('hello'))])))
    assert.equal(resultOf(await needsRefresh(local.store, 10, 's1', dir)).value, false)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    assert.equal(resultOf(await needsRefresh(local.store, 10, 's1', dir)).value, true)
    assert.equal(resultOf(await refreshCase(local.store, 's1', 'Q1b', 'A1b', toList([fileRead('a.txt', hash('changed'))]))).ok, true)
    assert.equal(resultOf(await fetchCase(local.store, 10, 's1')).value.A, 'A1b')
  } finally {
    local.close()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('CASE010_finalize_is_exactly_once_per_scope', async () => {
  const { CasebookWorkflow_finalizeCase: finalizeCase } = await import('../../../dist/Infrastructure/CasebookWorkflow.js')
  const local = createLocalEventStore()
  try {
    assert.equal(resultOf(await finalizeCase(local.store, caseRec('scope-1', 'Q', 'A', []))).ok, true)
    const second = resultOf(await finalizeCase(local.store, caseRec('scope-1', 'Q', 'A2', [])))
    assert.equal(second.ok, false)
    assert.match(second.error, /already finalized/)
    assert.equal(resultOf(await finalizeCase(local.store, caseRec('scope-2', 'Q', 'A', []))).ok, true)
  } finally {
    local.close()
  }
})
