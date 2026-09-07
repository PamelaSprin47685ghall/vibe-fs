// requirements/context-compression/tests/retry-policy.test.mjs — BloggerRetryPolicy + ProviderFailure budget.
//
// Retry policy and attempt planning:
// - Fixed participant role throughout execution
// - Single remote LLM target per role
// - Material-based retry policy: probe/squash is an immutable policy decision from journaled facts
// - Fresh physical attempt identity on retry; failure budget exhaustion stops auto-retries.
// - Plans carry no budget snapshot and no agent field.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

const requestKind = prefix.requestKind
const budget = failureOwner.budget

// ── Role and Remote LLM Invariance ──────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-006] role is fixed across retry attempts and consecutive failures', () => {
  const planner = compression.attemptPlanner

  const plan0 = planner.plan({ kind: requestKind.workMain, role: 'coder', policyAllowsProbe: false, noCandidateReason: 'NoCoverage' })
  const plan1 = planner.plan({ kind: requestKind.workMain, role: 'coder', policyAllowsProbe: false, noCandidateReason: 'NoCoverage' })
  const plan2 = planner.plan({ kind: requestKind.workMain, role: 'coder', policyAllowsProbe: true, noCandidateReason: 'NoCoverage' })

  assert.equal(plan0.participant, 'coder')
  assert.equal(plan1.participant, 'coder')
  assert.equal(plan2.participant, 'coder')

  assert.equal(plan0.participantIdentity.canonicalRole, 'coder')
  assert.equal(plan1.participantIdentity.canonicalRole, 'coder')
  assert.equal(plan2.participantIdentity.canonicalRole, 'coder')

  for (const plan of [plan0, plan1, plan2]) {
    assert.equal('budget' in plan, false, 'plans carry no budget snapshot')
    assert.equal('agent' in plan, false, 'plans carry no agent field')
  }
})

// ── Material-based retry policy ─────────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-006] retry policy includes squash only when BloggerMain fails and frames exist', () => {
  // BloggerMain failed with frames => next request is BloggerSquash
  assert.equal(compression.nextBloggerRequest('blogger-main', true), 'blogger-squash')

  // BloggerMain failed without frames => next request is BloggerMain
  assert.equal(compression.nextBloggerRequest('blogger-main', false), 'blogger-main')

  // BloggerSquash failed => always retries as BloggerMain (never infinite squash loops)
  assert.equal(compression.nextBloggerRequest('blogger-squash', true), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-squash', false), 'blogger-main')
})

test('WHAT[CONTEXT-COMPRESSION-008] only work_main requests may carry prefix probe', () => {
  assert.equal(requestKind.mayCarryProbe(requestKind.workMain), true)
  assert.equal(requestKind.mayCarryProbe(requestKind.bloggerMain), false)
  assert.equal(requestKind.mayCarryProbe(requestKind.bloggerSquash), false)
  assert.equal(requestKind.mayCarryProbe(requestKind.interactionRepair), false)
})

test('WHAT[CONTEXT-COMPRESSION-006] prefix probe is selected when policy allows and candidate exists', () => {
  const planner = compression.attemptPlanner

  const probe = {
    probeId: 'probe-alpha',
    basedOnEpoch: 0,
    candidate: prefix.snapshot({
      ref: 'blob-frozen-1',
      frozenDigest: 'frozen-1',
      cutoff: 1,
      prefixDigest: 'prefix-1',
      sealRoot: 'seal-1',
      syntheticId: 'synth-1',
    }),
  }

  const allowedWithProbe = planner.plan({
    kind: requestKind.workMain,
    policyAllowsProbe: true,
    probe,
  })
  assert.equal(allowedWithProbe.choice, 'UsePrefixProbe')
  assert.equal(allowedWithProbe.probeId, 'probe-alpha')

  const disallowed = planner.plan({
    kind: requestKind.workMain,
    policyAllowsProbe: false,
    probe,
  })
  assert.equal(disallowed.choice, 'UseCommittedEpoch')
  assert.equal(disallowed.probeId, null)
})

// ── Failure counting and budget exhaustion ──────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-006] consecutive failures consume the failure budget and exhaustion halts auto-retry', () => {
  let b = budget.initial
  assert.deepEqual(b, { failures: 0 })
  assert.equal(budget.verdict(budget.defaultBudget, b), 'MayRetry')

  for (let i = 1; i < budget.defaultBudget; i++) {
    b = budget.recordFailure(b)
    assert.equal(b.failures, i)
    assert.equal(budget.verdict(budget.defaultBudget, b), 'MayRetry')
  }

  // Budget reached
  b = budget.recordFailure(b)
  assert.equal(b.failures, budget.defaultBudget)
  assert.equal(budget.verdict(budget.defaultBudget, b), 'Exhausted')

  // Business main success clears failure count
  const cleared = budget.recordSuccess(b)
  assert.deepEqual(cleared, { failures: 0 })
  assert.equal(budget.verdict(budget.defaultBudget, cleared), 'MayRetry')
})

// ── Current owner surface shape ─────────────────────────────────────────────

test('WHAT[CONTEXT-COMPRESSION-006] compression owner exposes the retry dispatch and planning surface', () => {
  assert.equal(typeof compression.nextBloggerRequest, 'function')
  assert.equal(typeof compression.attemptPlanner.plan, 'function')
  assert.equal(typeof compression.attemptPlanner.promotableProbeId, 'function')
  assert.equal(typeof compression.terminalRequestOwnership, 'function')
})

test('WHAT[CONTEXT-COMPRESSION-002] retry dispatch reacts only to confirmed failure material', () => {
  // Without squash material the next request stays the ordinary main: squash
  // is never pre-selected before a confirmed failure produces material.
  assert.equal(compression.nextBloggerRequest('blogger-main', false), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-squash', false), 'blogger-main')
})

test('WHAT[CONTEXT-COMPRESSION-005] every recorded failure consumes exactly one budget unit', () => {
  // The budget API takes no reason or error text: failures are never classified.
  assert.equal(budget.recordFailure(budget.initial).failures, 1)
  assert.equal(budget.isValidRecord(0, 1), true)
  assert.equal(budget.isValidRecord(0, 2), false)
  assert.equal(budget.isValidRecord(3, 4), true)
})
