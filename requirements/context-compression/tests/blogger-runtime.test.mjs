// Moved from tests/unit/enforcer/blogger-runtime.test.mjs (cutover Wave 2a); owner: context-compression.
//
// ENFORCER-047: pure BloggerRuntime material routing + physical flight ownership.
// Busy = host HasFlight; material route = decideMaterial(hasParked, hasFlight, ctx).
import test from 'node:test'
import assert from 'node:assert/strict'
import * as owner from '../../../dist/Context/Companion/RuntimeSurface.js'
const ctx = owner
const rt = owner
const parkedTransform = owner

const main = () => ctx.main({ toml: 'work' })
const main2 = () => ctx.main({ toml: 'more' })
const KEY = 'ses-blog'

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_idle_plus_material_starts', () => {
  assert.equal(rt.decideMaterial(false, false, main()), 'Start')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_inflight_plus_material_skips_without_queue', () => {
  // hasFlight true → Skip; original flight ownership is not replaced by routing.
  assert.equal(rt.decideMaterial(false, true, main2()), 'Skip')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_cycle_commit_clears_flight', () => {
  const scope = parkedTransform.scope()
  parkedTransform.setCurrentRequest(scope, KEY, main())
  assert.equal(parkedTransform.hasFlight(scope, KEY), true)
  assert.equal(parkedTransform.peekCurrentRequest(scope, KEY)?.toml, 'work')

  parkedTransform.clearCurrentRequest(scope, KEY)
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
  assert.equal(parkedTransform.peekCurrentRequest(scope, KEY), null)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_idle_plus_parked_waiter_offers', () => {
  assert.equal(rt.decideMaterial(true, false, main2()), 'Offer')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_clear_flight_is_idempotent', () => {
  // Physical clear: second clear on empty ownership is a no-op (no NotInFlight cell error).
  const scope = parkedTransform.scope()
  parkedTransform.setCurrentRequest(scope, KEY, main())
  parkedTransform.clearCurrentRequest(scope, KEY)
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
  parkedTransform.clearCurrentRequest(scope, KEY)
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_clear_without_flight_is_idempotent', () => {
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
  parkedTransform.clearCurrentRequest(scope, KEY)
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_squash_commit_clears_flight', () => {
  // Squash commit path uses the same physical clear as cycle commit.
  const scope = parkedTransform.scope()
  parkedTransform.setCurrentRequest(scope, KEY, main())
  assert.equal(parkedTransform.hasFlight(scope, KEY), true)
  parkedTransform.clearCurrentRequest(scope, KEY)
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
  assert.equal(parkedTransform.peekCurrentRequest(scope, KEY), null)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_session_delete_is_registry_removal_not_a_cell_state', () => {
  // DSL-003: owner lifetime is the physical registry — session delete removes
  // flight ownership. There is no Disposed state tag.
  const scope = parkedTransform.scope()
  parkedTransform.setCurrentRequest(scope, KEY, main())
  assert.equal(parkedTransform.hasFlight(scope, KEY), true)
  parkedTransform.clearCurrentRequest(scope, KEY)
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_two_inflight_contexts_cannot_coexist', () => {
  // hasFlight already true → Skip; production keeps the registered flight.
  const scope = parkedTransform.scope()
  parkedTransform.setCurrentRequest(scope, KEY, main())
  assert.equal(rt.decideMaterial(false, true, main2()), 'Skip')
  assert.equal(parkedTransform.tryGetFlight(scope, KEY)?.toml, 'work')
  assert.notEqual(parkedTransform.tryGetFlight(scope, KEY)?.toml, 'more')
})

test('WHAT[CONTEXT-COMPRESSION-018] ENFORCER_047_waiter_offer_does_not_register_flight', () => {
  // DSL-003: Offer is routing only — parked host dictionary stages the context
  // (ENFORCER-050); decideMaterial(Offer) must not imply SetCurrentRequest.
  assert.equal(rt.decideMaterial(true, false, main2()), 'Offer')
  const scope = parkedTransform.scope()
  assert.equal(parkedTransform.hasFlight(scope, KEY), false)
})
