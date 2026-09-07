// Moved from tests/unit/enforcer/blogger-seal-reactivate.test.mjs (cutover Wave 2a); owner: context-compression.
//
// Blogger seal after join/return + clean owner boundary (R05/R06).
// Sealed stays sealed; fake drain latch deleted. Busy = physical request lease.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as bloggerRuntime from '../../../dist/Context/Companion/RuntimeSurface.js'
import * as parkedTransform from '../../../dist/Context/Companion/RuntimeSurface.js'
import * as handle from '../../../dist/Execution/Delegation/Handle/Surface.js'

const KEY = 'ses-blog'
const ctx = () => bloggerRuntime.main({
  requestId: 'request-main',
  mainSession: 'ses-main',
  bloggerSession: KEY,
  toml: '[[new_work_to_record]]\nuser = "work"',
  previousIngested: 0,
  nextIngested: 1,
  previousCutoff: 0,
  nextCutoff: 1,
  nextDigest: 'digest-1',
  frameEpoch: 0,
  deltaDigest: 'delta-1',
  observedEpoch: 0,
})

test('WHAT[CONTEXT-COMPRESSION-018] HANDLE_lifecycle_CompletedAwaitingJoin_and_Retired_seal_blogger', () => {
  const completed = handle.scenario('complete')
  assert.equal(completed.ok, true)
  assert.equal(completed.record.lifecycle, 'CompletedAwaitingJoin')

  const retired = handle.scenario('retire')
  assert.equal(retired.ok, true)
  assert.equal(retired.record.lifecycle, 'Retired')
})

test('WHAT[CONTEXT-COMPRESSION-018] HANDLE_lifecycle_Abandoned_seals_blogger', () => {
  const abandoned = handle.scenario('abandon')
  assert.equal(abandoned.ok, true)
  assert.equal(abandoned.record.lifecycle, 'Abandoned')
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_cell_has_no_sealed_mirror_durable_is_truth', async () => {
  // R05: DrainWindow deleted; sealed stays sealed, new root starts fresh owner scope.
  const scope = parkedTransform.scope()
  parkedTransform.claimCurrentRequest(scope, KEY, ctx())
  assert.notEqual(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_park_and_offer_material_mailbox', async () => {
  // Mailbox delivery: park awaits material, offer resumes waiter
  const scope = parkedTransform.scope()
  const waiter = parkedTransform.park(scope, KEY)
  assert.equal(parkedTransform.offerMaterial(scope, KEY, ctx()), 'Delivered')
  const wake = await waiter
  assert.equal(wake.kind, 'MaterialAvailable')
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_staged_offer_delivers_to_next_park', async () => {
  // Offer first stages, then park consumes immediately
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.offerMaterial(scope, KEY, ctx()), 'Staged')
  const wake = await parkedTransform.park(scope, KEY)
  assert.equal(wake.kind, 'MaterialAvailable')
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_flight_lease_claim_and_release', () => {
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, ctx()), 'Claimed')
  assert.notEqual(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
})
