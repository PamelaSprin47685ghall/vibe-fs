import test from 'node:test'
import assert from 'node:assert/strict'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

const budget = failureOwner.budget

const projection = failureOwner.providerFailureProjection

test('WHAT[context-compression-022] CTX_022_retry_sequence_returns_to_main_through_one_formula', () => {
  // Main with material -> Squash -> Main -> Main: the squash detour always
  // rejoins the main path instead of following a second retry formula.
  assert.equal(compression.nextBloggerRequest('blogger-main', true), 'blogger-squash')
  assert.equal(compression.nextBloggerRequest('blogger-squash', true), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-main', false), 'blogger-main')
})

test('WHAT[context-compression-022] CTX_022_durable_projection_keeps_newest_failure_count', () => {
  let state = projection.forAuthority('run-newest-covers', 'root-1')

  const first = projection.applyFailure(budget.attemptIdentity('ses-1', 'run-newest-covers', 'root-1', 'provider-run-1'), 1, state)
  assert.equal(first.ok, true)
  assert.equal(projection.read(first.value).failures, 1)

  const second = projection.applyFailure(
    budget.attemptIdentity('ses-1', 'run-newest-covers', 'root-1', 'provider-run-2'),
    2,
    first.value,
  )
  assert.equal(second.ok, true)
  assert.equal(projection.read(second.value).failures, 2)

  // Replaying an already observed physical attempt is refused, never double-counted.
  const replay = projection.applyFailure(
    budget.attemptIdentity('ses-1', 'run-newest-covers', 'root-1', 'provider-run-1'),
    1,
    second.value,
  )
  assert.equal(replay.ok, false)
  assert.equal(replay.error, 'AlreadyObserved')
  assert.equal(projection.read(second.value).failures, 2)

  // Success on the business path resets the durable count.
  assert.equal(projection.read(projection.recordSuccess(second.value)).failures, 0)
})

test('WHAT[context-compression-022] CTX_022_projection_retry_follows_the_failure_budget', () => {
  let state = projection.forAuthority('run-budget-follows', 'root-1')
  assert.equal(projection.mayRetry(budget.defaultBudget, state), true)

  for (let count = 1; count <= budget.defaultBudget; count++) {
    const applied = projection.applyFailure(
      budget.attemptIdentity('ses-1', 'run-budget-follows', 'root-1', `provider-run-${count}`),
      count,
      state,
    )
    assert.equal(applied.ok, true)
    state = applied.value
  }

  assert.equal(projection.read(state).failures, budget.defaultBudget)
  assert.equal(projection.mayRetry(budget.defaultBudget, state), false)
})
