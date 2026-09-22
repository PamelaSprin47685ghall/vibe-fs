import assert from 'node:assert/strict'
import test from 'node:test'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as attemptPurpose from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'
import * as reconcile from '../../../dist/Composition/Turn/ReconcileSurface.js'

const planner = compression.attemptPlanner

const { budget, providerFailureProjection } = failureOwner

const TOOL_CAPABILITIES = [
  'BashHoneypot',
  'Edit',
  'Fetch',
  'Fission',
  'Glob',
  'Grep',
  'Move',
  'Read',
  'Remove',
  'Sphinx',
  'Write',
]

test('WHAT[provider-attempt-recovery-018] recovery_retry_unlocks_only_on_durable_material_without_waiters', () => {
  // Material-driven: the same failed kind plus the same material presence
  // always decides the same next request — no transient waiter, timer or
  // clock state participates in the decision.
  for (const [failedKind, hasMaterial, expected] of [
    ['blogger-main', true, 'blogger-squash'],
    ['blogger-main', false, 'blogger-main'],
    ['blogger-squash', true, 'blogger-main'],
    ['blogger-squash', false, 'blogger-main'],
  ]) {
    assert.equal(compression.nextBloggerRequest(failedKind, hasMaterial), expected)
    assert.equal(compression.nextBloggerRequest(failedKind, hasMaterial), expected)
  }

  // With no durable open producer (no squash material) the physical retry
  // proceeds at once as Main — it never waits for future material.
  assert.equal(compression.nextBloggerRequest('blogger-main', false), 'blogger-main')

  // Timers, deadlines, sleeps, polling and process-local waiter state take
  // no part in the wait: the decision surface carries no such channel.
  for (const absent of ['StartRecoveryOpportunity', 'OfferRecoveryMaterial', 'recoveryWaiter']) {
    assert.equal(typeof compression[absent], 'undefined', `${absent} must stay absent from CompressionSurface`)
  }
})
