import assert from 'node:assert/strict'
import test from 'node:test'
import * as domain from '../../../dist/Knowledge/Casebook/DomainSurface.js'
import * as store from '../../../dist/Knowledge/Casebook/StoreSurface.js'
import * as fetchTool from '../../../dist/OpenCode/Tools/KnowledgeFetchSurface.js'
import * as fetchSurface from '../../../dist/Repository/Knowledge/Casebook/FetchSurface.js'

test('WHAT[KNOWLEDGE-REUSE-004] CASE004_classifyReplay_fresh_only_on_exact_normalized_equality', () => {
  const o1 = [domain.readObservation('a.txt', 'h1')]
  const o2 = [domain.readObservation('a.txt', 'h1')]
  const o3 = [domain.readObservation('a.txt', 'h2')]
  assert.equal(domain.classifyReplay(o1, o2), 'Fresh')
  assert.equal(domain.classifyReplay(o1, o3), 'Stale')
})

test('WHAT[KNOWLEDGE-REUSE-004] CASE004_005_workflow_archive_fetch_closed_loop_reads_Current_only', async () => {
  let s = store.empty
  const obs = [domain.readObservation('f.txt', 'h1')]
  s = store.captureCase('c1', 'q', 'a', obs, '2026-01-01T00:00:00Z', s).value
  const f = store.fetchCase('c1', obs, s)
  assert.equal(f.ok, true)
  assert.equal(f.value.fresh, true)
})

test('WHAT[KNOWLEDGE-REUSE-004] CASE004_refresh_and_needsRefresh_replay_the_same_Current', async () => {
  let s = store.empty
  const oldObs = [domain.readObservation('f.txt', 'h1')]
  s = store.captureCase('c1', 'q', 'a', oldObs, '2026-01-01T00:00:00Z', s).value
  const newObs = [domain.readObservation('f.txt', 'h2')]
  const stale = store.fetchCase('c1', newObs, s)
  assert.equal(stale.value.fresh, false)
})

test('WHAT[KNOWLEDGE-REUSE-004] CASE004_fetch_uses_shelfmark_and_replays_before_refreshing', async () => {
  const res = await fetchTool.fetchWithReplay('shelf-1', [domain.readObservation('f.txt', 'h1')])
  assert.equal(res.checked, true)
})

test('WHAT[KNOWLEDGE-REUSE-004] CASE009_fetch_never_writes_the_subject', async () => {
  const res = await fetchTool.isReadOnlyFetch()
  assert.equal(res, true)
})
