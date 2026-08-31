import assert from 'node:assert/strict'
import test from 'node:test'

import * as transaction from '../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js'

const evidence = {
  sessionId: 'ses-transaction',
  physicalUserMessageId: 'msg-transaction',
  logicalRunId: 'run-transaction',
  authorityRootUserMessageId: 'root-transaction',
  authorityKind: 'HumanRoot',
  identitySeed: {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: 'fast-coder',
      peerAgent: 'deep-coder',
      canonicalRole: 'coder',
      selectedTier: 'fast',
      persona: 'Coder',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  },
  providerRun: 'provider-transaction',
  origin: 'HumanRoot',
  effectiveAgent: 'fast-coder',
  requestKind: 'work-main',
  projectionChoice: { kind: 'UseCommittedEpoch' },
}

const run = (failurePoint = 'None', state = 'None') =>
  transaction.transactionScenario(evidence, failurePoint, state)

test('WHAT[CHATEXEC-003] managed admission has one fixed success order', async () => {
  const result = await run()

  assert.equal(result.ok, true, JSON.stringify(result.error))
  assert.equal(result.outcome, 'Settled')
  assert.deepEqual(result.trace, [
    'ResolveState',
    'Accept',
    'AcceptedWitness',
    'AcquireLease',
    'LeaseTarget',
    'BindExecution',
    'ProjectHost',
    'CommitLease',
    'Settled',
  ])
  assert.deepEqual(result.target, { model: 'openai/gpt-5', reasoning: 'high' })
  assert.equal(result.acceptCount, 1)
  assert.equal(result.acquireCount, 1)
  assert.equal(result.bindCount, 1)
  assert.equal(result.hostCount, 1)
  assert.equal(result.commitCount, 1)
  assert.equal(result.releaseCount, 0)
  assert.equal(result.providerCount, 0)
})

test('WHAT[CHATEXEC-003] append failure performs zero downstream effects', async () => {
  for (const failurePoint of ['AcceptNotAttempted', 'AcceptCommitUnknown']) {
    const result = await run(failurePoint)

    assert.equal(result.ok, false)
    assert.equal(result.error.kind, failurePoint === 'AcceptNotAttempted' ? 'NotAttempted' : 'CommitUnknown')
    assert.deepEqual(result.trace, ['ResolveState', 'Accept'])
    assert.equal(result.acquireCount, 0)
    assert.equal(result.bindCount, 0)
    assert.equal(result.hostCount, 0)
    assert.equal(result.commitCount, 0)
    assert.equal(result.releaseCount, 0)
    assert.equal(result.providerCount, 0)
  }
})

test('WHAT[CHATEXEC-003] acquisition failure crosses no later boundary', async () => {
  const result = await run('AcquireLease')

  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'LeaseAcquisitionFailed')
  assert.deepEqual(result.trace, [
    'ResolveState',
    'Accept',
    'AcceptedWitness',
    'AcquireLease',
    'TerminalizeAccepted',
  ])
  assert.equal(result.bindCount, 0)
  assert.equal(result.hostCount, 0)
  assert.equal(result.commitCount, 0)
  assert.equal(result.releaseCount, 0)
  assert.equal(result.providerCount, 0)
})

test('WHAT[CHATEXEC-003] superseded demand is a typed nonfatal short-circuit', async () => {
  const result = await run('AcquireSuperseded')

  assert.equal(result.ok, true, JSON.stringify(result.error))
  assert.equal(result.outcome, 'Superseded')
  assert.deepEqual(result.trace, [
    'ResolveState',
    'Accept',
    'AcceptedWitness',
    'AcquireLease',
    'TerminalizeAccepted',
  ])
  assert.equal(result.bindCount, 0)
  assert.equal(result.hostCount, 0)
  assert.equal(result.commitCount, 0)
  assert.equal(result.releaseCount, 0)
  assert.equal(result.providerCount, 0)
})

test('WHAT[EMR-013] queue full and cancellation cross no bind Host or provider boundary', async () => {
  for (const [failurePoint, outcome] of [
    ['AcquireQueueFull', 'CapacityQueueFull'],
    ['AcquireCancelled', 'Cancelled'],
  ]) {
    const result = await run(failurePoint)

    assert.equal(result.ok, true, JSON.stringify(result.error))
    assert.equal(result.outcome, outcome)
    assert.deepEqual(result.trace, [
      'ResolveState',
      'Accept',
      'AcceptedWitness',
      'AcquireLease',
      'TerminalizeAccepted',
    ])
    assert.equal(result.bindCount, 0)
    assert.equal(result.hostCount, 0)
    assert.equal(result.commitCount, 0)
    assert.equal(result.releaseCount, 0)
    assert.equal(result.providerCount, 0)
  }
})

test('WHAT[CHATEXEC-007] every acquired pre-commit failure releases exactly once', async () => {
  const expectations = new Map([
    ['LeaseTarget', 'LeaseTargetFailed'],
    ['BindExecution', 'BindingFailed'],
    ['ProjectHost', 'HostProjectionFailed'],
    ['CommitLease', 'LeaseCommitFailed'],
  ])

  for (const [failurePoint, errorKind] of expectations) {
    const result = await run(failurePoint)

    assert.equal(result.ok, false)
    assert.equal(result.error.kind, errorKind)
    assert.equal(result.releaseCount, 1)
    assert.equal(result.commitCount, failurePoint === 'CommitLease' ? 1 : 0)
    assert.equal(result.providerCount, 0)
    assert.equal(result.trace.at(-1), 'ReleaseBeforeProvider')
    assert.ok(result.trace.indexOf('TerminalizeAccepted') < result.trace.indexOf('ReleaseBeforeProvider'))
    assert.ok(result.trace.indexOf('UnbindExecution') < result.trace.indexOf('ReleaseBeforeProvider'))

    if (failurePoint === 'BindExecution') {
      assert.equal(result.hostCount, 0)
    }

    if (failurePoint === 'ProjectHost') {
      assert.equal(result.commitCount, 0)
    }
  }
})

test('WHAT[CHATEXEC-007] release boundary failure is typed without a second release', async () => {
  const result = await run('ReleaseBeforeProvider')

  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'BindingFailed')
  assert.equal(result.error.release, 'BoundaryFailed')
  assert.equal(result.releaseCount, 1)
  assert.equal(result.hostCount, 0)
  assert.equal(result.commitCount, 0)
  assert.equal(result.providerCount, 0)
})

test('WHAT[CHATEXEC-004] accepted replay reuses acceptance without another append', async () => {
  const result = await run('None', 'Accepted')

  assert.equal(result.ok, true, JSON.stringify(result.error))
  assert.equal(result.outcome, 'Settled')
  assert.equal(result.acceptCount, 1)
  assert.equal(result.appendCount, 0)
  assert.equal(result.acquireCount, 1)
  assert.equal(result.providerCount, 0)
})

test('WHAT[CHATEXEC-006] terminal replay performs no acceptance or capacity effect', async () => {
  const result = await run('None', 'Terminal')

  assert.equal(result.ok, true, JSON.stringify(result.error))
  assert.equal(result.outcome, 'AlreadyTerminal')
  assert.deepEqual(result.trace, ['ResolveState'])
  assert.equal(result.acceptCount, 0)
  assert.equal(result.acquireCount, 0)
  assert.equal(result.bindCount, 0)
  assert.equal(result.hostCount, 0)
  assert.equal(result.providerCount, 0)
})

test('WHAT[CHATEXEC-003] already-started replay performs no duplicate admission effect', async () => {
  const result = await run('None', 'ProviderStarted')

  assert.equal(result.ok, true, JSON.stringify(result.error))
  assert.equal(result.outcome, 'AlreadyStarted')
  assert.deepEqual(result.trace, ['ResolveState'])
  assert.equal(result.acceptCount, 0)
  assert.equal(result.acquireCount, 0)
  assert.equal(result.bindCount, 0)
  assert.equal(result.hostCount, 0)
  assert.equal(result.providerCount, 0)
})
