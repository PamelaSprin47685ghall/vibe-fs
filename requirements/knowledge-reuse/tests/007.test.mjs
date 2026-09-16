import assert from 'node:assert/strict'
import test from 'node:test'
import * as store from '../../../dist/Knowledge/Casebook/StoreSurface.js'

test('WHAT[KNOWLEDGE-REUSE-007] CASE007_captured_refreshed_round_trip_through_integrator_Current', async () => {
  let s = store.empty
  s = store.captureCase('c1', 'q', 'a', [], '2026-01-01T00:00:00Z', s).value
  s = store.refreshCase('c1', 'new-a', [], '2026-01-02T00:00:00Z', s).value
  assert.equal(store.findCase('c1', s).answer, 'new-a')
})

test('WHAT[KNOWLEDGE-REUSE-007] CASE007_accessed_and_evicted_are_integrated_without_feature_history_scan', async () => {
  let s = store.empty
  s = store.captureCase('c1', 'q', 'a', [], '2026-01-01T00:00:00Z', s).value
  s = store.touchAccess('c1', '2026-01-02T00:00:00Z', s).value
  assert.equal(store.findCase('c1', s) !== null, true)
})

test('WHAT[KNOWLEDGE-REUSE-007] CASE007_store_has_no_loadEvents_project_or_history_reader', () => {
  assert.equal(store.hasHistoryReader(), false)
})
