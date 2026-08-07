/**
 * ENFORCER-047/050/160/162 — typed offer context + park primitive.
 */
import test from 'node:test'
import assert from 'node:assert/strict'
import { bloggerRequestContext as ctx, parkedTransform } from '../support/domain.mjs'

const SHORT_LIFETIME_MS = 200
const main = (toml = 'delta-1') => ctx.main({ toml })

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
  const resumed = parkedTransform.offerParked(scope, 'ses-blogger', main('delta-1'))
  assert.equal(resumed, false)
  assert.equal(parkedTransform.hasParked(scope, 'ses-blogger'), false)

  const waiter = parkedTransform.park(scope, 'ses-blogger', 60_000)
  assert.equal(await waiter, true)
  const staged = parkedTransform.consumeStaged(scope, 'ses-blogger')
  assert.equal(staged.kind, 'Main')
  assert.equal(staged.toml, 'delta-1')
  assert.equal(parkedTransform.consumeStaged(scope, 'ses-blogger'), undefined)
})

test('ENFORCER_050_an_offer_to_a_parked_transform_resumes_it_with_the_context', async () => {
  const scope = parkedTransform.scope()
  const waiter = parkedTransform.park(scope, 'ses-blogger', 60_000)

  const resumed = parkedTransform.offerParked(scope, 'ses-blogger', main('delta-2'))
  assert.equal(resumed, true)
  assert.equal(await waiter, true)
  const staged = parkedTransform.consumeStaged(scope, 'ses-blogger')
  assert.equal(staged.kind, 'Main')
  assert.equal(staged.toml, 'delta-2')
})

test('ENFORCER_050_staged_context_is_consumed_only_once', async () => {
  const scope = parkedTransform.scope()
  parkedTransform.offerParked(scope, 'ses-blogger', main('once'))
  const first = parkedTransform.consumeStaged(scope, 'ses-blogger')
  const second = parkedTransform.consumeStaged(scope, 'ses-blogger')
  assert.equal(first.toml, 'once')
  assert.equal(second, undefined)
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

test('ENFORCER_162_cancel_clears_staged_context_with_or_without_waiter', async () => {
  const scope = parkedTransform.scope()

  // Staged only (offer-first, no park yet).
  parkedTransform.offerParked(scope, 'ses-x', main('staged'))
  parkedTransform.cancelParked(scope, 'ses-x')
  assert.equal(parkedTransform.consumeStaged(scope, 'ses-x'), undefined)

  // Parked + staged: cancel releases waiter and drops the offer.
  const waiter = parkedTransform.park(scope, 'ses-blogger', 60_000)
  parkedTransform.offerParked(scope, 'ses-blogger', main('gone'))
  // offer already resumed the waiter; a second stage then cancel:
  parkedTransform.offerParked(scope, 'ses-blogger', main('again'))
  parkedTransform.cancelParked(scope, 'ses-blogger')
  assert.equal(parkedTransform.consumeStaged(scope, 'ses-blogger'), undefined)
  assert.equal(await waiter, true)
})

test('ENFORCER_161_sessions_are_independent_under_the_same_scope', async () => {
  const scope = parkedTransform.scope()
  const a = parkedTransform.park(scope, 'ses-a', 60_000)

  const b = parkedTransform.park(scope, 'ses-b', 60_000)
  parkedTransform.resumeParked(scope, 'ses-b')
  assert.equal(await b, true)
  assert.equal(parkedTransform.hasParked(scope, 'ses-a'), true)

  parkedTransform.cancelParked(scope, 'ses-a')
  assert.equal(await a, false)
})

test('ENFORCER_047_CurrentRequest_is_physical_flight_ownership', () => {
  // PR7 D6: flight registry is the sole ownership authority (no State.InFlight shadow).
  const scope = parkedTransform.scope()
  const key = 'ses-blogger'
  const request = main('coverage-delta')

  assert.equal(parkedTransform.peekCurrentRequest(scope, key), undefined)
  assert.equal(parkedTransform.hasFlight(scope, key), false)

  parkedTransform.setCurrentRequest(scope, key, request)
  const peeked = parkedTransform.peekCurrentRequest(scope, key)
  assert.equal(peeked?.kind, 'Main')
  assert.equal(peeked?.toml, 'coverage-delta')
  assert.equal(parkedTransform.hasFlight(scope, key), true)
  assert.equal(parkedTransform.tryGetFlight(scope, key)?.toml, 'coverage-delta')

  // Commit success path: ClearCurrentRequest drops ownership.
  parkedTransform.clearCurrentRequest(scope, key)
  assert.equal(parkedTransform.peekCurrentRequest(scope, key), undefined)
  assert.equal(parkedTransform.hasFlight(scope, key), false)

  // Fail path: Clear while live removes flight (idempotent thereafter).
  parkedTransform.setCurrentRequest(scope, key, main('fail-me'))
  assert.equal(parkedTransform.hasFlight(scope, key), true)
  parkedTransform.clearCurrentRequest(scope, key)
  assert.equal(parkedTransform.peekCurrentRequest(scope, key), undefined)
  assert.equal(parkedTransform.hasFlight(scope, key), false)
})
