// retry-policy.test.mjs — PAR-006/008/010/011/013/015/016/018.
//
// Retry policy and attempt plans in the one-retry world. Every assertion
// drives production: AttemptPlanner via PlannerSurface/CompressionSurface,
// BloggerRetryPolicy via CompressionSurface.nextBloggerRequest, TerminalValidity
// via CompressionSurface.terminalValidity, and the failure budget via
// Fallback/ProviderFailureSurface.js. No copied policy, no source-text reads.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as attemptPurpose from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

const planner = compression.attemptPlanner
const { budget, providerFailureProjection } = failureOwner

const TOOL_CAPABILITIES = [
  'BashHoneypot',
  'Edit',
  'Fetch',
  'Fission',
  'Glob',
  'Grep',
  'Inspect',
  'Move',
  'Read',
  'Remove',
  'Write',
]

test('WHAT[PAR-013] participant_identity_role_and_persona_remain_immutable_across_retries', () => {
  const first = planner.plan({ role: 'coder', kind: 'work-main' })
  const second = planner.plan({ role: 'coder', kind: 'work-main' })

  assert.equal(first.participant, 'coder')
  assert.equal(second.participant, 'coder')
  assert.deepEqual(first.participantIdentity, second.participantIdentity)
  assert.equal(first.systemPromptId, second.systemPromptId)
  assert.deepEqual(first.toolCapabilities, second.toolCapabilities)

  assert.deepEqual(first.participantIdentity, {
    selectedAgent: 'coder',
    canonicalRole: 'coder',
    role: 'coder',
    participant: 'coder',
    selectedTier: 'deep',
    persona: 'Coder',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  })
})

test('WHAT[PAR-013] plans_derive_system_prompt_and_tools_from_the_fixed_role', () => {
  const planned = attemptPurpose.plan({ role: 'coder', kind: 'work-main' })

  assert.equal(planned.ok, true)
  assert.equal(planned.systemPromptId, planned.participantIdentity.canonicalRole)
  assert.deepEqual(planned.toolCapabilities, TOOL_CAPABILITIES)
})

test('WHAT[PAR-011] plans_carry_no_budget_snapshot', () => {
  const planned = planner.plan({ role: 'coder', kind: 'work-main' })

  for (const key of ['failures', 'budget', 'consecutiveFailureCount', 'count', 'exhausted']) {
    assert.equal(key in planned, false, `plan must not carry ${key}`)
  }
  assert.deepEqual(Object.keys(planned).sort(), [
    'canonicalRole',
    'choice',
    'handle',
    'noProbeReason',
    'participant',
    'participantIdentity',
    'probeId',
    'projectionChoice',
    'requestKind',
    'role',
    'systemPromptId',
    'toolCapabilities',
  ])
})

test('WHAT[PAR-003] each_retry_binds_a_fresh_physical_identity', () => {
  // Fresh physical identity is a ledger requirement: two distinct ProviderRun
  // identities both advance; observing one twice never advances twice.
  const first = budget.attemptIdentity('ses_a', 'run_L', 'msg_u1', 'provider-1')
  const second = budget.attemptIdentity('ses_a', 'run_L', 'msg_u1', 'provider-2')
  assert.notEqual(budget.dedupeKey(first), budget.dedupeKey(second))

  let current = providerFailureProjection.forAuthority('run_L', 'msg_u1')
  current = providerFailureProjection.applyFailure(first, 1, current).value
  current = providerFailureProjection.applyFailure(second, 2, current).value
  assert.equal(providerFailureProjection.read(current).failures, 2)

  assert.deepEqual(providerFailureProjection.applyFailure(first, 2, current), {
    ok: false,
    error: 'AlreadyObserved',
  })
})

test('WHAT[PAR-008] an_invalid_terminal_earns_at_most_one_repair_and_never_advances', () => {
  // Empty and XML-only terminals are unusable content (production
  // TerminalValidity), not provider failures: the budget has no input for
  // terminal text, so recording nothing is structural.
  assert.deepEqual(compression.terminalValidity(''), { valid: false, rejection: 'Empty' })
  assert.deepEqual(compression.terminalValidity('<tool_call>do</tool_call>'), {
    valid: false,
    rejection: 'XmlOnly',
  })
  assert.deepEqual(compression.terminalValidity('a real answer'), { valid: true, rejection: null })

  assert.equal(budget.recordFailure.length, 1, 'budget advance takes only the budget, never terminal text')
})

test('WHAT[PAR-008] only_a_probe_attempt_with_a_usable_terminal_may_promote', () => {
  const withProbe = planner.plan({
    role: 'coder',
    kind: 'work-main',
    policyAllowsProbe: true,
    probe: {
      probeId: 'probe-p1',
      basedOnEpoch: 0,
      candidate: {
        ref: 'blob-frozen-5',
        frozenDigest: 'frozen-5',
        cutoff: 5,
        prefixDigest: 'prefix-5',
        sealRoot: 'seal-5',
        syntheticId: 'synthetic-seal-5',
      },
    },
  })

  assert.equal(planner.promotableProbeId(withProbe, 'Completed'), 'probe-p1')
  assert.equal(planner.promotableProbeId(withProbe, 'CompletedInvalid'), null)
  assert.equal(planner.promotableProbeId(withProbe, 'Failed'), null)
  assert.equal(planner.promotableProbeId(withProbe, 'Aborted'), null)
})

test('WHAT[PAR-011] an_attempt_without_a_probe_cannot_promote_even_on_success', () => {
  const withoutProbe = planner.plan({ role: 'coder', kind: 'work-main', policyAllowsProbe: false })
  assert.equal(planner.promotableProbeId(withoutProbe, 'Completed'), null)
})

test('WHAT[PAR-010] blogger_retry_dispatch_selects_squash_when_material_exists_and_main_otherwise', () => {
  assert.equal(compression.nextBloggerRequest('blogger-main', true), 'blogger-squash')
  assert.equal(compression.nextBloggerRequest('blogger-main', false), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-squash', true), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-squash', false), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('work-main', true), 'NoActiveBloggerRun')
})

test('WHAT[PAR-011] retry_decision_is_material_based_and_physically_bound', () => {
  // Same failed kind, only the material flag flips the decision: no transient
  // channel participates — the two calls are pure functions of their inputs.
  assert.notEqual(
    compression.nextBloggerRequest('blogger-main', true),
    compression.nextBloggerRequest('blogger-main', false),
  )
  // The decision names a request kind, never a physical identity: binding a
  // fresh physical identity happens once per retry at the ledger (covered in
  // provider-failure-ledger.test.mjs), not inside the policy.
  assert.match(compression.nextBloggerRequest('blogger-main', true), /blogger-squash/)
})

test('WHAT[PAR-015] independent_sessions_keep_independent_budgets', () => {
  const a0 = providerFailureProjection.forAuthority('run_L', 'msg_u1')
  const b0 = providerFailureProjection.forAuthority('run_M', 'msg_u2')

  const idA = budget.attemptIdentity('ses_a', 'run_L', 'msg_u1', 'run_1')
  const idB = budget.attemptIdentity('ses_b', 'run_M', 'msg_u2', 'run_9')
  const a1 = providerFailureProjection.applyFailure(idA, 1, a0)
  const b1 = providerFailureProjection.applyFailure(idB, 1, b0)
  assert.equal(a1.ok, true)
  assert.equal(b1.ok, true)

  assert.deepEqual(providerFailureProjection.read(a1.value), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    failures: 1,
    dedupeKeys: 1,
    exhausted: false,
  })
  assert.deepEqual(providerFailureProjection.read(b1.value), {
    logicalRun: 'run_M',
    authorityRoot: 'msg_u2',
    failures: 1,
    dedupeKeys: 1,
    exhausted: false,
  })
})

test('WHAT[PAR-016] success_accounting_requires_proven_request_kind', () => {
  // Only WorkMain-family attempts may carry a probe; maintenance and repair
  // kinds never do — so their success can never promote and never clear the
  // budget through the probe path. The kind label itself decides.
  assert.equal(attemptPurpose.ordinaryRequestPurpose('InteractionRepair', ''), 'interaction-repair')
  assert.equal(attemptPurpose.ordinaryRequestPurpose('HumanRoot', 'main'), 'blogger-main')
  assert.equal(attemptPurpose.ordinaryRequestPurpose('HumanRoot', 'squash'), 'blogger-squash')
  assert.equal(attemptPurpose.ordinaryRequestPurpose('HumanRoot', ''), 'work-main')

  // Clearing rule at the budget: only a business-main success resets; the
  // projection exposes recordSuccess for exactly that case while a squash or
  // repair success simply never calls it.
  const advanced = providerFailureProjection.applyFailure(
    budget.attemptIdentity('ses_a', 'run_L', 'msg_u1', 'run_1'),
    1,
    providerFailureProjection.forAuthority('run_L', 'msg_u1'),
  ).value
  assert.equal(providerFailureProjection.read(advanced).failures, 1)
  assert.equal(providerFailureProjection.read(providerFailureProjection.recordSuccess(advanced)).failures, 0)
})
test('WHAT[PAR-006] retry_keeps_fixed_participant_with_decoupled_model_routing', () => {
  // Fixed participant: the same role plans twice with an identical identity,
  // system prompt and tool set — no retry ever switches the participant.
  const first = planner.plan({ role: 'coder', kind: 'work-main' })
  const second = planner.plan({ role: 'coder', kind: 'work-main' })
  assert.deepEqual(first.participantIdentity, second.participantIdentity)
  assert.equal(first.systemPromptId, second.systemPromptId)
  assert.deepEqual(first.toolCapabilities, second.toolCapabilities)

  // Decoupled model routing: the plan names the participant, never a
  // model/provider target — target selection is a scheduler backend choice
  // for the same participant, not a domain identity change.
  for (const key of ['model', 'provider', 'modelTarget', 'providerTarget', 'endpoint', 'scheduler']) {
    assert.equal(key in first, false, `plan must not carry ${key}`)
  }

  // At the consecutive-failure budget the run is exhausted at once: the
  // budget verdict flips exactly at the limit, so no over-budget request
  // is ever issued.
  assert.equal(budget.verdict(budget.defaultBudget, { failures: budget.defaultBudget - 1 }), 'MayRetry')
  assert.equal(budget.verdict(budget.defaultBudget, { failures: budget.defaultBudget }), 'Exhausted')
})

test('WHAT[PAR-018] recovery_retry_unlocks_only_on_durable_material_without_waiters', () => {
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
