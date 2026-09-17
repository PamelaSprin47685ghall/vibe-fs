import assert from 'node:assert/strict'
import test from 'node:test'
import * as compression from '../../../dist/Context/Companion/CompressionSurface.js'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as failureOwner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

const requestKind = prefix.requestKind

const budget = failureOwner.budget

test('WHAT[CONTEXT-COMPRESSION-006] role is fixed across retry attempts and consecutive failures', () => {
  const planner = compression.attemptPlanner

  const plan0 = planner.plan({ kind: requestKind.workMain, role: 'engineer', policyAllowsProbe: false, noCandidateReason: 'NoCoverage' })
  const plan1 = planner.plan({ kind: requestKind.workMain, role: 'engineer', policyAllowsProbe: false, noCandidateReason: 'NoCoverage' })
  const plan2 = planner.plan({ kind: requestKind.workMain, role: 'engineer', policyAllowsProbe: true, noCandidateReason: 'NoCoverage' })

  assert.equal(plan0.participant, 'engineer')
  assert.equal(plan1.participant, 'engineer')
  assert.equal(plan2.participant, 'engineer')

  assert.equal(plan0.participantIdentity.canonicalRole, 'engineer')
  assert.equal(plan1.participantIdentity.canonicalRole, 'engineer')
  assert.equal(plan2.participantIdentity.canonicalRole, 'engineer')

  for (const plan of [plan0, plan1, plan2]) {
    assert.equal('budget' in plan, false, 'plans carry no budget snapshot')
    assert.equal('agent' in plan, false, 'plans carry no agent field')
  }
})

test('WHAT[CONTEXT-COMPRESSION-006] retry policy includes squash only when BloggerMain fails and frames exist', () => {
  // BloggerMain failed with frames => next request is BloggerSquash
  assert.equal(compression.nextBloggerRequest('blogger-main', true), 'blogger-squash')

  // BloggerMain failed without frames => next request is BloggerMain
  assert.equal(compression.nextBloggerRequest('blogger-main', false), 'blogger-main')

  // BloggerSquash failed => always retries as BloggerMain (never infinite squash loops)
  assert.equal(compression.nextBloggerRequest('blogger-squash', true), 'blogger-main')
  assert.equal(compression.nextBloggerRequest('blogger-squash', false), 'blogger-main')
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

test('WHAT[CONTEXT-COMPRESSION-006] compression owner exposes the retry dispatch and planning surface', () => {
  assert.equal(typeof compression.nextBloggerRequest, 'function')
  assert.equal(typeof compression.attemptPlanner.plan, 'function')
  assert.equal(typeof compression.attemptPlanner.promotableProbeId, 'function')
  assert.equal(typeof compression.terminalRequestOwnership, 'function')
})
