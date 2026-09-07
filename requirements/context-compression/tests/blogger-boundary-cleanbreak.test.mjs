// Sealed/mailbox/flight regression tests (owner: context-compression).
// - Sealed stays sealed: cancel wakes waiter, release clears flight (018)
// - Material mailbox: material-first vs waiter-first vs newest-covers (018)
// - Flight claim/release lease: exact RequestId-scoped lease, foreign flight conflict, idempotent release, owner dispose (024/018)
import test from 'node:test'
import assert from 'node:assert/strict'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'

const KEY = 'ses-blogger-test'
const reqCtx = (reqId, toml = 'test') => runtime.main({
  requestId: reqId,
  mainSession: 'ses-main',
  bloggerSession: KEY,
  toml,
  previousIngested: 0,
  nextIngested: 1,
  previousCutoff: 0,
  nextCutoff: 1,
  nextDigest: 'digest-1',
  frameEpoch: 0,
  deltaDigest: 'delta-1',
  observedEpoch: 0,
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_sealed_stays_sealed_cancel_wakes_waiter_and_release_clears_flight', async () => {
  const scope = runtime.scope()
  const ctxA = reqCtx('req-1', 'initial')

  // Claim initial flight
  assert.equal(runtime.claimCurrentRequest(scope, KEY, ctxA), 'Claimed')
  assert.notEqual(runtime.currentRequest(scope, KEY), null)

  // Dispose/cancel cancels waiters
  const waiter = runtime.park(scope, KEY)
  runtime.cancelParked(scope, KEY)
  const wake = await waiter
  assert.equal(wake.kind, 'Cancelled')

  // Releasing current request frees flight
  assert.equal(runtime.releaseCurrentRequest(scope, KEY, 'req-1'), 'Released')
  assert.equal(runtime.currentRequest(scope, KEY), null)
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_material_mailbox_waiter_first_delivers_directly', async () => {
  const scope = runtime.scope()
  const ctx = reqCtx('req-waiter-first', 'waiter-content')

  const waiter = runtime.park(scope, KEY)
  assert.equal(runtime.offerMaterial(scope, KEY, ctx), 'Delivered')
  const wake = await waiter
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.toml, 'waiter-content')
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_material_mailbox_material_first_stages_for_next_park', async () => {
  const scope = runtime.scope()
  const ctx = reqCtx('req-mat-first', 'staged-content')

  assert.equal(runtime.offerMaterial(scope, KEY, ctx), 'Staged')
  const wake = await runtime.park(scope, KEY)
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.toml, 'staged-content')
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_material_mailbox_newest_covers_supersedes_older_offer', async () => {
  const scope = runtime.scope()
  const ctx1 = reqCtx('req-1', 'older-material')
  const ctx2 = reqCtx('req-2', 'newest-material')

  assert.equal(runtime.offerMaterial(scope, KEY, ctx1), 'Staged')
  assert.equal(runtime.offerMaterial(scope, KEY, ctx2), 'Staged')

  // Next park gets the newest material, older is cleanly covered
  const wake = await runtime.park(scope, KEY)
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.toml, 'newest-material')
})

test('WHAT[CONTEXT-COMPRESSION-024] CTX_024_flight_claim_conflict_keeps_foreign_owner_and_rejects_stale_release', () => {
  const scope = runtime.scope()
  const ctx1 = reqCtx('req-owner-1', 'content-1')
  const ctx2 = reqCtx('req-owner-2', 'content-2')

  assert.equal(runtime.claimCurrentRequest(scope, KEY, ctx1), 'Claimed')
  assert.equal(runtime.claimCurrentRequest(scope, KEY, ctx2), 'Conflict:req-owner-1')
  assert.equal(runtime.currentRequest(scope, KEY).toml, 'content-1')

  // Stale release from owner-2 rejected
  assert.equal(runtime.releaseCurrentRequest(scope, KEY, 'req-owner-2'), 'Conflict:req-owner-1')
  assert.equal(runtime.currentRequest(scope, KEY).toml, 'content-1')

  // Owner-1 releases successfully
  assert.equal(runtime.releaseCurrentRequest(scope, KEY, 'req-owner-1'), 'Released')
  assert.equal(runtime.currentRequest(scope, KEY), null)
})

test('WHAT[CONTEXT-COMPRESSION-018] BLOGGER_RUNTIME_flight_lease_dispose_clears_exact_flight', () => {
  const scope = runtime.scope()
  const ctx1 = reqCtx('req-lease-1', 'content-lease')

  const lease = runtime.claimFlight(scope, KEY, ctx1)
  assert.notEqual(lease, null)
  assert.notEqual(runtime.currentRequest(scope, KEY), null)

  // Dispose lease clears flight
  lease.Dispose()
  assert.equal(runtime.currentRequest(scope, KEY), null)
})
