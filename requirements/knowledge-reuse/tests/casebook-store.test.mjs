// FROZEN — 2026-08-14. Shock-cut to canonical Integrator Current.
// Intentionally NOT executed before implementation.

import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync, mkdirSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { EventStoreSurface_create as createEventStore, EventStoreSurface_dispose as disposeEventStore } from '../../../dist/Persistence/EventStore/Surface.js'
import * as casebook from '../../../dist/Repository/Knowledge/Casebook/Surface.js'

const fileRead = (path, contentHash) => ({ kind: 'file-read', path, contentHash })
const globResult = (pattern, paths) => ({ kind: 'glob-result', pattern, paths })

const createCasebookEventStore = () => {
  const commonDir = mkdtempSync(join(tmpdir(), 'wxs-casebook-store-'))
  const store = createEventStore(commonDir, 'casebook-test-writer')
  return {
    store,
    close: () => {
      disposeEventStore(store)
      rmSync(commonDir, { recursive: true, force: true })
    },
  }
}

const unwrap = async (operation) => {
  const result = await operation
  assert.equal(result.ok, true, `expected successful Casebook operation, got ${JSON.stringify(result.error)}`)
  return result.value
}

const caseRec = (sessionId, q, a, observations) => ({
  sessionId,
  q,
  a,
  observations,
  lastAccessOrder: 0,
})

const findCase = async (store, sessionId) => {
  const result = await casebook.fetchCase(store, 10, sessionId)
  assert.equal(result.ok, true, JSON.stringify(result.error))
  return result.value
}

test('WHAT[KNOWLEDGE-REUSE-007] CASE007_captured_refreshed_round_trip_through_integrator_Current', async () => {
  const local = createCasebookEventStore()
  try {
    await unwrap(casebook.archive(local.store, caseRec('s1', 'Q1', 'A1', [fileRead('a.txt', 'h1')])))
    await unwrap(casebook.refresh(local.store, 's1', 'Q1b', 'A1b', [fileRead('a.txt', 'h1'), globResult('*.fs', ['x'])]))
    const s1 = await findCase(local.store, 's1')
    assert.equal(s1.a, 'A1b')
    assert.equal(s1.observations.length, 2)
  } finally {
    local.close()
  }
})

test('WHAT[KNOWLEDGE-REUSE-007] CASE007_accessed_and_evicted_are_integrated_without_feature_history_scan', async () => {
  const local = createCasebookEventStore()
  try {
    await unwrap(casebook.archive(local.store, caseRec('s1', 'Q', 'A', [])))
    const before = (await findCase(local.store, 's1')).lastAccessOrder
    await unwrap(casebook.touchAccess(local.store, 's1'))
    assert.ok((await findCase(local.store, 's1')).lastAccessOrder >= before)
    await unwrap(casebook.evictCase(local.store, 's1'))
    assert.equal(await findCase(local.store, 's1'), null)
  } finally {
    local.close()
  }
})

test('WHAT[KNOWLEDGE-REUSE-007] CASE007_store_has_no_loadEvents_project_or_history_reader', async () => {
  const { readFileSync } = await import('node:fs')
  const source = readFileSync(new URL('../../../src/Wanxiangshu/Repository/Knowledge/Casebook/Store.fs', import.meta.url), 'utf8')
  assert.doesNotMatch(source, /loadEvents|loadEnvelopes|project\s*\(|OpenSnapshot|readStreams/)
  assert.match(source, /tryDecodeEnvelope/)
})

test('WHAT[KNOWLEDGE-REUSE-009] CASE009_marker_gates_the_surface', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-cbmarker-'))
  try {
    assert.equal(casebook.featureEnabled(dir), false)
    mkdirSync(join(dir, '.wanxiang', 'casebook'), { recursive: true })
    assert.equal(casebook.featureEnabled(dir), true)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-004] CASE004_005_workflow_archive_fetch_closed_loop_reads_Current_only', async () => {
  const local = createCasebookEventStore()
  try {
    await unwrap(casebook.archive(local.store, caseRec('s1', 'Q1', 'A1', [fileRead('a.txt', 'h1')])))
    const fetched = await findCase(local.store, 's1')
    assert.equal(fetched.a, 'A1')
  } finally {
    local.close()
  }
})

test('WHAT[KNOWLEDGE-REUSE-005] CASE004_005_freshness_check_is_hint_not_proof_reads_Current_only', async () => {
  const local = createCasebookEventStore()
  try {
    await unwrap(casebook.archive(local.store, caseRec('s1', 'Q1', 'A1', [fileRead('a.txt', 'h1')])))
    const fetched = await findCase(local.store, 's1')
    assert.equal(casebook.classifyReplay(fetched.observations, [fileRead('a.txt', 'h1')]), 'fresh')
    assert.equal(casebook.classifyReplay(fetched.observations, [fileRead('a.txt', 'h2')]), 'stale')
  } finally {
    local.close()
  }
})

test('WHAT[KNOWLEDGE-REUSE-004] CASE004_refresh_and_needsRefresh_replay_the_same_Current', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-cbrefresh-'))
  const local = createCasebookEventStore()
  try {
    writeFileSync(join(dir, 'a.txt'), 'hello', 'utf8')
    await unwrap(casebook.archive(local.store, caseRec('s1', 'Q1', 'A1', [fileRead('a.txt', '2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824')])))
    assert.equal((await casebook.needsRefresh(local.store, 10, 's1', dir)).value, false)
    writeFileSync(join(dir, 'a.txt'), 'changed', 'utf8')
    assert.equal((await casebook.needsRefresh(local.store, 10, 's1', dir)).value, true)
    await unwrap(casebook.refresh(local.store, 's1', 'Q1b', 'A1b', [fileRead('a.txt', 'd67e2e944994496c8d8ec76eed0cf9f09679448d584b532bebf941852a37f5ed')]))
    assert.equal((await findCase(local.store, 's1')).a, 'A1b')
  } finally {
    local.close()
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[KNOWLEDGE-REUSE-010] CASE010_finalize_is_exactly_once_per_scope', async () => {
  const local = createCasebookEventStore()
  try {
    assert.equal((await casebook.finalize(local.store, caseRec('scope-1', 'Q', 'A', []))).ok, true)
    const second = await casebook.finalize(local.store, caseRec('scope-1', 'Q', 'A2', []))
    assert.equal(second.ok, false)
    assert.match(second.error, /already finalized/)
    assert.equal((await casebook.finalize(local.store, caseRec('scope-2', 'Q', 'A', []))).ok, true)
  } finally {
    local.close()
  }
})
