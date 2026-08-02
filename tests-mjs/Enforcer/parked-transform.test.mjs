/**
 * ENFORCER-160/162 + STRENGTH-078 C-04/C-05/C-09 — the parkable transform
 * primitive. Layer 1: pure runtime semantics, no Host.
 *
 * The Host contract (transform awaits are real suspensions, per-session
 * isolation, cancel-on-dispose) is proved by the layer-4 canary
 * `host-transform-capability`; here we lock the runtime cell itself:
 *  - park resolves true on resume, false on cancel;
 *  - a timed-out park resolves false (fail closed, never hang);
 *  - an offer staged before the park is consumed by the park (offer-first
 *    merge, ENFORCER-050);
 *  - an offer to a parked transform resumes it and stages the injection;
 *  - cancel removes the entry so a later park is a fresh one;
 *  - dispose cancels every parked waiter.
 */
import test from 'node:test'
import assert from 'node:assert/strict'
import { parkedTransform } from '../domain.mjs'

const SHORT_LIFETIME_MS = 200

test('ENFORCER_160_park_resolves_true_on_resume_and_removes_the_entry', async () => {
  const scope = parkedTransform.scope()
  const waiter = parkedTransform.park(scope, 'ses-blogger', 60_000)
  assert.equal(parkedTransform.hasParked(scope, 'ses-blogger'), true)

  const resumed = parkedTransform.resumeParked(scope, 'ses-blogger')
  assert.equal(resumed, true)
  assert.equal(await waiter, true)
  assert.equal(parkedTransform.hasParked(scope, 'ses-blogger'), false)
})

test('ENFORCER_162_cancel_resolves_false_and_releases_the_waiter', async () => {
  const scope = parkedTransform.scope()
  const waiter = parkedTransform.park(scope, 'ses-blogger', 60_000)

  parkedTransform.cancelParked(scope, 'ses-blogger')
  assert.equal(await waiter, false)
  assert.equal(parkedTransform.hasParked(scope, 'ses-blogger'), false)
})

test('ENFORCER_160_a_timed_out_park_resolves_false_fail_closed', async () => {
  const scope = parkedTransform.scope()
  const waiter = parkedTransform.park(scope, 'ses-blogger', SHORT_LIFETIME_MS)

  assert.equal(await waiter, false)
  assert.equal(parkedTransform.hasParked(scope, 'ses-blogger'), false)
})

test('ENFORCER_050_an_offer_staged_before_the_park_is_consumed_by_the_park', async () => {
  const scope = parkedTransform.scope()
  // Offer first — no transform parked yet (ENFORCER-050 skip branch).
  const resumed = parkedTransform.offerParked(scope, 'ses-blogger', 'delta-1')
  assert.equal(resumed, false)
  assert.equal(parkedTransform.hasParked(scope, 'ses-blogger'), false)

  // Park later — the staged offer is waiting, so the park returns true with
  // the injection already consumable.
  const waiter = parkedTransform.park(scope, 'ses-blogger', 60_000)
  assert.equal(await waiter, true)
  assert.equal(parkedTransform.consumeStaged(scope, 'ses-blogger'), 'delta-1')
  assert.equal(parkedTransform.consumeStaged(scope, 'ses-blogger'), undefined)
})

test('ENFORCER_050_an_offer_to_a_parked_transform_resumes_it_with_the_injection', async () => {
  const scope = parkedTransform.scope()
  const waiter = parkedTransform.park(scope, 'ses-blogger', 60_000)

  const resumed = parkedTransform.offerParked(scope, 'ses-blogger', 'delta-2')
  assert.equal(resumed, true)
  assert.equal(await waiter, true)
  assert.equal(parkedTransform.consumeStaged(scope, 'ses-blogger'), 'delta-2')
})

test('ENFORCER_160_two_parks_for_one_session_share_one_waiter', async () => {
  const scope = parkedTransform.scope()
  const first = parkedTransform.park(scope, 'ses-blogger', 60_000)
  const second = parkedTransform.park(scope, 'ses-blogger', 60_000)

  assert.equal(parkedTransform.resumeParked(scope, 'ses-blogger'), true)
  assert.equal(await first, true)
  assert.equal(await second, true)
  assert.equal(parkedTransform.hasParked(scope, 'ses-blogger'), false)
})

test('ENFORCER_162_dispose_cancels_every_parked_waiter', async () => {
  const scope = parkedTransform.scope()
  const a = parkedTransform.park(scope, 'ses-a', 60_000)
  const b = parkedTransform.park(scope, 'ses-b', 60_000)

  parkedTransform.dispose(scope)
  assert.equal(await a, false)
  assert.equal(await b, false)
  assert.equal(parkedTransform.hasParked(scope, 'ses-a'), false)
  assert.equal(parkedTransform.hasParked(scope, 'ses-b'), false)
})

test('ENFORCER_161_sessions_are_independent_under_the_same_scope', async () => {
  const scope = parkedTransform.scope()
  const a = parkedTransform.park(scope, 'ses-a', 60_000)

  // Park B and resume it — A stays parked.
  const b = parkedTransform.park(scope, 'ses-b', 60_000)
  parkedTransform.resumeParked(scope, 'ses-b')
  assert.equal(await b, true)
  assert.equal(parkedTransform.hasParked(scope, 'ses-a'), true)

  parkedTransform.cancelParked(scope, 'ses-a')
  assert.equal(await a, false)
})
