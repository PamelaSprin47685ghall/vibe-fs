import assert from 'node:assert/strict'
import test from 'node:test'
import * as store from '../../../dist/Knowledge/Casebook/StoreSurface.js'
import * as domain from '../../../dist/Knowledge/Casebook/DomainSurface.js'
import * as bookkeeper from '../../../dist/Knowledge/Bookkeeper/SessionSurface.js'

test('WHAT[KNOWLEDGE-REUSE-005] CASE006_missing_runtime_keeps_old_case', async () => {
  let s = store.empty
  s = store.captureCase('c1', 'q', 'old-ans', [], '2026-01-01T00:00:00Z', s).value
  const refreshed = await bookkeeper.runRefreshWithFallback('c1', s, null)
  assert.equal(refreshed.answer, 'old-ans')
})

test('WHAT[KNOWLEDGE-REUSE-005] CASE004_005_freshness_check_is_hint_not_proof_reads_Current_only', async () => {
  let s = store.empty
  const obs = [domain.readObservation('f.txt', 'h1')]
  s = store.captureCase('c1', 'q', 'a', obs, '2026-01-01T00:00:00Z', s).value
  const res = store.fetchCase('c1', obs, s)
  assert.equal(res.value.isHintOnly, true)
})
