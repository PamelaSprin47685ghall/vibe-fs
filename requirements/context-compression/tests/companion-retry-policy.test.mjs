// requirements/context-compression/tests/companion-retry-policy.test.mjs — Blogger retry dispatch,
// durable ProviderFailure material, and exact live repair ownership through compiled surfaces.

import test from 'node:test'
import assert from 'node:assert/strict'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

const budget = failureOwner.budget
const projection = failureOwner.providerFailureProjection

// ── BloggerRetryPolicy: material-driven retry dispatch ──────────────────────

test('WHAT[CONTEXT-COMPRESSION-021] CTX_021_failed_blogger_main_with_material_dispatches_squash', () => {
  assert.equal(compression.nextBloggerRequest('blogger-main', true), 'blogger-squash')
  assert.equal(compression.nextBloggerRequest('blogger-main', false), 'blogger-main')
})

test('WHAT[CONTEXT-COMPRESSION-021] CTX_021_failed_squash_always_retries_as_main', () => {
  assert.equal(compression.nextBloggerRequest('blogger-squash', true), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-squash', false), 'blogger-main')
})

test('WHAT[CONTEXT-COMPRESSION-021] CTX_021_retry_dispatch_distinguishes_missing_projection_from_no_active_run', () => {
  assert.equal(compression.nextBloggerRequest('missing', true), 'MissingProjection')
  assert.equal(compression.nextBloggerRequest('work-main', true), 'NoActiveBloggerRun')
})

test('WHAT[CONTEXT-COMPRESSION-007] same_failed_kind_with_same_material_always_selects_the_same_next_request', () => {
  // (Also WHAT[PAR-018]: the retry continuation decides immediately from durable
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

// ── Single retry formula + newest-covers durable material ───────────────────

test('WHAT[CONTEXT-COMPRESSION-022] CTX_022_retry_sequence_returns_to_main_through_one_formula', () => {
  // Main with material -> Squash -> Main -> Main: the squash detour always
  // rejoins the main path instead of following a second retry formula.
  assert.equal(compression.nextBloggerRequest('blogger-main', true), 'blogger-squash')
  assert.equal(compression.nextBloggerRequest('blogger-squash', true), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-main', false), 'blogger-main')
})

test('WHAT[CONTEXT-COMPRESSION-022] CTX_022_durable_projection_keeps_newest_failure_count', () => {
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

test('WHAT[CONTEXT-COMPRESSION-022] CTX_022_projection_retry_follows_the_failure_budget', () => {
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

// ── Exact live repair ownership ─────────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-024] CTX_024_request_scoped_repair_continues_only_for_the_current_request', () => {
  const base = {
    requestId: 'req-new',
    openRequestId: 'req-new',
    openPromptKey: 'prompt-new',
  }

  assert.equal(
    compression.terminalRequestOwnership({
      ...base,
      parentPromptKey: 'prompt-repair',
      parentOrigin: 'InteractionRepair',
      parentPayloadDigest: 'req-new\u001fmsg-old\u001fblogger-missing-tool',
    }),
    'Current',
  )

  assert.equal(
    compression.terminalRequestOwnership({
      ...base,
      parentPromptKey: 'prompt-repair-old',
      parentOrigin: 'InteractionRepair',
      parentPayloadDigest: 'req-old\u001fmsg-old\u001fblogger-missing-tool',
    }),
    'Superseded',
  )

  assert.equal(compression.terminalRequestOwnership({ requestId: 'req-unproven' }), 'Unproven')
})
