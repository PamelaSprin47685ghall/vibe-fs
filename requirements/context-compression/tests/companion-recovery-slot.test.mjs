/**
 * CTX-006 / FALLBACK-012: Companion recovery opportunity is a one-shot physical waiter.
 *
 * Failure opens `StartRecoveryOpportunity` (registers a TCS). Material offers via
 * `OfferRecoveryMaterial` consume that waiter once. No Armed/NotArmed program
 * counter — opportunity exists while the waiter Task is unfinished. A fresh
 * Companion has no residual opportunity (restart-safe).
 */
import test from 'node:test'
import assert from 'node:assert/strict'
import { sessionId } from '../../verification-system/tests/support/domain.mjs'

const Companion = await import('../../../dist/Context/Companion/Runtime.js')

const make = () => Object.entries(Companion).find(([k]) => k.startsWith('Companion_$ctor'))?.[1](undefined, undefined, sessionId('ses-main'))

const startOpportunity = (c) => Companion.Companion__StartRecoveryOpportunity(c)
const offerMaterial = (c) => Companion.Companion__OfferRecoveryMaterial(c)

test('WHAT[CONTEXT-COMPRESSION-006] CTX_006_fresh_companion_has_no_recovery_opportunity', () => {
  const c = make()
  assert.equal(offerMaterial(c), false, 'no register → offer is no-op')
})

test('WHAT[CONTEXT-COMPRESSION-006] CTX_006_start_then_offer_consumes_waiter_once', async () => {
  const c = make()
  const opportunity = startOpportunity(c)
  assert.equal(offerMaterial(c), true, 'first offer takes the waiter')
  await opportunity
  assert.equal(offerMaterial(c), false, 'second offer no longer consumed')
})

test('WHAT[CONTEXT-COMPRESSION-006] CTX_006_second_start_reuses_single_opportunity', async () => {
  const c = make()
  const first = startOpportunity(c)
  const second = startOpportunity(c)
  // Same one-shot waiter: two starts share one opportunity.
  assert.equal(offerMaterial(c), true)
  await Promise.all([first, second])
  assert.equal(offerMaterial(c), false)
})

test('WHAT[CONTEXT-COMPRESSION-006] CTX_006_offer_without_register_is_noop', () => {
  const c = make()
  assert.equal(offerMaterial(c), false)
  assert.equal(offerMaterial(c), false)
})
