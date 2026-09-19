import test from 'node:test'
import assert from 'node:assert/strict'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

const budget = failureOwner.budget

const projection = failureOwner.providerFailureProjection

test('WHAT[context-compression-007] same_failed_kind_with_same_material_always_selects_the_same_next_request', () => {
  // (Also WHAT[provider-attempt-recovery-018]: the retry continuation decides immediately from durable
  // material — it never parks on a waiter for future production.)
  // Same failed kind + same material presence always selects the same next
  // request: the decision carries no cross-attempt waiter state.
  for (const [failedKind, hasMaterial, expected] of [
    ['blogger-main', true, 'blogger-squash'],
    ['blogger-main', false, 'blogger-main'],
    ['blogger-squash', true, 'blogger-main'],
    ['blogger-squash', false, 'blogger-main'],
  ]) {
    assert.equal(compression.nextBloggerRequest(failedKind, hasMaterial), expected)
    assert.equal(compression.nextBloggerRequest(failedKind, hasMaterial), expected)
  }

  for (const absent of [
    'StartRecoveryOpportunity',
    'OfferRecoveryMaterial',
    'recoveryWaiter',
  ]) {
    assert.equal(typeof compression[absent], 'undefined', `${absent} must stay absent from CompressionSurface`)
  }
})
