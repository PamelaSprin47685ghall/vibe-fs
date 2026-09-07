// Moved from tests/unit/enforcer/blogger-runtime.test.mjs (cutover Wave 2a); owner: context-compression.
//
// ENFORCER-047: pure BloggerRuntime material routing + physical flight ownership.
// Busy = exact shared flight lease; material route also preserves one durable open producer.
import test from 'node:test'
import assert from 'node:assert/strict'
import * as owner from '../../../dist/Context/Companion/RuntimeSurface.js'
const ctx = owner
const parkedTransform = owner

const main = () => ctx.main({ toml: 'work' })
const main2 = () => ctx.main({ toml: 'more' })
const KEY = 'ses-blog'

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_idle_plus_material_starts', () => {
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, main()), 'Claimed')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY)?.toml, 'work')
  parkedTransform.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_inflight_plus_material_skips_without_queue', () => {
  // hasFlight true → Skip; original flight ownership is not replaced by routing.
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, ctx.main({ requestId: 'req-first', toml: 'work' })), 'Claimed')
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, ctx.main({ requestId: 'req-second', toml: 'more' })), 'Conflict:req-first')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY)?.toml, 'work')
  parkedTransform.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_open_producer_between_steps_stages_material', () => {
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.offerParked(scope, KEY, main2()), 'Staged')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_cycle_commit_clears_flight', () => {
  const scope = parkedTransform.scope()
  parkedTransform.claimCurrentRequest(scope, KEY, main())
  assert.notEqual(parkedTransform.tryGetFlight(scope, KEY), null)
  assert.equal(parkedTransform.peekCurrentRequest(scope, KEY)?.toml, 'work')

  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
  assert.equal(parkedTransform.peekCurrentRequest(scope, KEY), null)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_idle_plus_parked_waiter_offers', async () => {
  const scope = parkedTransform.scope()
  const waiter = parkedTransform.park(scope, KEY)
  assert.equal(parkedTransform.offerParked(scope, KEY, main2()), 'Delivered')
  const wake = await waiter
  assert.equal(wake.kind, 'MaterialAvailable')
  assert.equal(wake.context.toml, 'more')
  parkedTransform.dispose(scope)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_clear_flight_is_idempotent', () => {
  // Physical clear: second clear on empty ownership is a no-op (no NotInFlight cell error).
  const scope = parkedTransform.scope()
  parkedTransform.claimCurrentRequest(scope, KEY, main())
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_clear_without_flight_is_idempotent', () => {
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
})

test('WHAT[PAR-017] Blogger retry replaces exact physical ownership before the next binding', () => {
  const scope = parkedTransform.scope()
  const failed = ctx.main({ requestId: 'request-failed', toml: 'failed' })
  const replacement = ctx.main({ requestId: 'request-replacement', toml: 'replacement' })

  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, failed), 'Claimed')
  assert.equal(parkedTransform.releaseCurrentRequest(scope, KEY, 'request-failed'), 'Released')
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, replacement), 'Claimed')
  assert.equal(parkedTransform.releaseCurrentRequest(scope, KEY, 'request-failed'), 'Conflict:request-replacement')
  assert.equal(parkedTransform.peekCurrentRequest(scope, KEY)?.toml, 'replacement')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_squash_commit_clears_flight', () => {
  // Squash commit path uses the same physical clear as cycle commit.
  const scope = parkedTransform.scope()
  parkedTransform.claimCurrentRequest(scope, KEY, main())
  assert.notEqual(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
  assert.equal(parkedTransform.peekCurrentRequest(scope, KEY), null)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_session_delete_is_registry_removal_not_a_cell_state', () => {
  // DSL-003: owner lifetime is the physical registry — session delete removes
  // flight ownership. There is no Disposed state tag.
  const scope = parkedTransform.scope()
  parkedTransform.claimCurrentRequest(scope, KEY, main())
  assert.notEqual(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.releaseCurrentRequest(scope, KEY, 'request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_two_inflight_contexts_cannot_coexist', () => {
  // hasFlight already true → Skip; production keeps the registered flight.
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, main()), 'Claimed')
  assert.equal(parkedTransform.claimCurrentRequest(scope, KEY, ctx.main({ requestId: 'req-second', toml: 'more' })), 'Conflict:request-main')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY)?.toml, 'work')
  assert.notEqual(parkedTransform.tryGetFlight(scope, KEY)?.toml, 'more')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_waiter_offer_does_not_register_flight', () => {
  // DSL-003: Offer is routing only — parked host dictionary stages the context
  // (ENFORCER-050); a staged offer must not imply SetCurrentRequest.
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.offerParked(scope, KEY, main2()), 'Staged')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY), null)
  parkedTransform.dispose(scope)
})
