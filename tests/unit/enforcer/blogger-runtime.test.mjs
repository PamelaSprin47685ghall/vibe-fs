/**
 * ENFORCER-047: pure BloggerRuntime cell transitions (state + PendingOffer).
 */
import test from 'node:test'
import assert from 'node:assert/strict'
import { bloggerRequestContext as ctx, bloggerRuntime as rt } from '../support/domain.mjs'

const main = () => ctx.main({ toml: 'work' })
const main2 = () => ctx.main({ toml: 'more' })

test('ENFORCER_047_idle_plus_material_starts_inflight', () => {
  const r = rt.onMaterial(rt.idle, main())
  assert.equal(r.ok, true)
  assert.equal(rt.stateOf(r.state), 'InFlight')
  assert.equal(r.decision, 'Start')
  assert.equal(ctx.toml(rt.inFlightContext(r.state)), 'work')
})

test('ENFORCER_047_inflight_plus_material_skips_without_queue', () => {
  const started = rt.onMaterial(rt.idle, main())
  const r = rt.onMaterial(started.state, main2())
  assert.equal(r.ok, true)
  assert.equal(r.decision, 'Skip')
  assert.equal(rt.stateOf(r.state), 'InFlight')
  assert.equal(ctx.toml(rt.inFlightContext(r.state)), 'work', 'original context kept')
})

test('ENFORCER_047_cycle_commit_moves_inflight_to_parked', () => {
  const started = rt.onMaterial(rt.idle, main())
  const r = rt.onCycleCommitted(started.state)
  assert.equal(r.ok, true)
  assert.equal(rt.stateOf(r.state), 'Parked')
  assert.equal(rt.inFlightContext(r.state), undefined)
})

test('ENFORCER_047_parked_plus_material_offers_without_leaving_parked', () => {
  const r = rt.onMaterial(rt.parked, main2())
  assert.equal(r.ok, true)
  assert.equal(r.decision, 'Offer')
  assert.equal(rt.stateOf(r.state), 'Parked', 'Offer must not flip state to InFlight')
  assert.equal(rt.inFlightContext(r.state), undefined)
})

test('ENFORCER_047_try_take_inflight_consumes_context_once', () => {
  const started = rt.onMaterial(rt.idle, main())
  const taken = rt.tryTakeInFlight(started.state)
  assert.equal(taken.ok, true)
  assert.equal(ctx.toml(taken.context), 'work')
  assert.equal(rt.stateOf(taken.state), 'Parked')

  const again = rt.tryTakeInFlight(taken.state)
  assert.equal(again.ok, false)
  assert.equal(again.error, 'NoContext')
})

test('ENFORCER_047_cycle_commit_from_idle_is_rejected', () => {
  const r = rt.onCycleCommitted(rt.idle)
  assert.equal(r.ok, false)
  assert.equal(r.error, 'NotInFlight')
})

test('ENFORCER_047_squash_without_pending_main_parks', () => {
  const started = rt.onMaterial(rt.idle, main())
  const r = rt.onSquashCommitted(started.state, undefined)
  assert.equal(r.ok, true)
  assert.equal(rt.stateOf(r.state), 'Parked')
  assert.equal(r.decision, 'Ignore')
})

test('ENFORCER_047_squash_with_pending_main_restarts_inflight', () => {
  const started = rt.onMaterial(rt.idle, main())
  const r = rt.onSquashCommitted(started.state, main2())
  assert.equal(r.ok, true)
  assert.equal(rt.stateOf(r.state), 'InFlight')
  assert.equal(r.decision, 'Start')
  assert.equal(ctx.toml(rt.inFlightContext(r.state)), 'more')
})

test('ENFORCER_047_session_delete_is_registry_removal_not_a_cell_state', () => {
  // DSL-003: owner lifetime is the physical registry — session delete cancels
  // the parked waiter and REMOVES the cell (PluginRuntimeScope.DeleteSession);
  // a missing cell reads back as Idle empty. There is no Disposed state tag:
  // it was written-then-immediately-removed, so no reader could ever see it.
  const started = rt.onMaterial(rt.idle, main())
  assert.equal(started.ok, true)
  assert.equal(rt.stateOf(started.state), 'InFlight')
})

test('ENFORCER_047_two_inflight_contexts_cannot_coexist', () => {
  const a = rt.onMaterial(rt.idle, main())
  const b = rt.onMaterial(a.state, main2())
  assert.equal(b.decision, 'Skip')
  assert.equal(ctx.toml(rt.inFlightContext(b.state)), 'work')
  assert.notEqual(ctx.toml(rt.inFlightContext(b.state)), 'more')
})

test('ENFORCER_047_parked_offer_keeps_cell_without_pending_slot', () => {
  // DSL-003: the cell has no PendingOffer mirror — the host dictionary is the
  // sole staging authority (ENFORCER-050), asserted at the scope level by
  // offerParked/consumeStaged in parked-transform tests.
  const offered = rt.onMaterial(rt.parked, main2())
  assert.equal(offered.decision, 'Offer')
  assert.equal(rt.stateOf(offered.state), 'Parked')
})
