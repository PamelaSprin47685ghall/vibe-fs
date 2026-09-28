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
      selectedAgent: 'engineer', canonicalRole: 'engineer', selectedTier: 'deep',
      persona: 'Engineer', personaCatalogVersion: 1, origin: 'ResolvedAtRoot',
    },
  },
  providerRun: 'provider-transaction',
  origin: 'HumanRoot', requestKind: 'work-main',
  projectionChoice: { kind: 'UseCommittedEpoch' },
}
const run = (failure = 'None', state = 'None') => transaction.transactionScenario(evidence, failure, state)
const beforeAcquisition = ['ResolveState', 'Accept', 'AcceptedWitness', 'AcquireLease']
const assertNoLaterEffects = (result) => {
  for (const name of ['bindCount', 'hostCount', 'commitCount', 'releaseCount', 'providerCount']) {
    assert.equal(result[name], 0, name)
  }
}

test('WHAT[managed-chat-execution-003] production admission orders its controlled ports before reporting Settled', async () => {
  const result = await run()
  assert.equal(result.ok, true, JSON.stringify(result.error))
  assert.equal(result.outcome, 'Settled')
  assert.deepEqual(result.trace, [
    ...beforeAcquisition, 'LeaseTarget', 'BindExecution', 'ProjectHost', 'CommitLease', 'Settled',
  ])
  assert.deepEqual(result.target, { model: 'openai/gpt-5', reasoning: 'high' })
  for (const name of ['acceptCount', 'acquireCount', 'bindCount', 'hostCount', 'commitCount']) {
    assert.equal(result[name], 1, name)
  }
  assert.equal(result.releaseCount, 0)
  assert.equal(result.providerCount, 0)
})

test('WHAT[managed-chat-execution-003] uncertain or unattempted acceptance performs zero downstream effects', async () => {
  for (const [failure, kind] of [['AcceptNotAttempted', 'NotAttempted'], ['AcceptCommitUnknown', 'CommitUnknown']]) {
    const result = await run(failure)
    assert.equal(result.ok, false)
    assert.equal(result.error.kind, kind)
    assert.deepEqual(result.trace, ['ResolveState', 'Accept'])
    assert.equal(result.acquireCount, 0)
    assertNoLaterEffects(result)
  }
})

test('WHAT[managed-chat-execution-003] acquisition failure settles Accepted and crosses no later boundary', async () => {
  const result = await run('AcquireLease')
  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'LeaseAcquisitionFailed')
  assert.deepEqual(result.trace, [...beforeAcquisition, 'TerminalizeAccepted'])
  assertNoLaterEffects(result)
})

test('WHAT[managed-chat-execution-003] supersession, queue full and cancellation stop after exact settlement', async () => {
  for (const [failure, outcome] of [
    ['AcquireSuperseded', 'Superseded'], ['AcquireQueueFull', 'CapacityQueueFull'], ['AcquireCancelled', 'Cancelled'],
  ]) {
    const result = await run(failure)
    assert.equal(result.ok, true, JSON.stringify(result.error))
    assert.equal(result.outcome, outcome)
    assert.deepEqual(result.trace, [...beforeAcquisition, 'TerminalizeAccepted'])
    assertNoLaterEffects(result)
  }
})

test('WHAT[managed-chat-execution-003] already-started replay performs no duplicate admission effect', async () => {
  const result = await run('None', 'ProviderStarted')
  assert.equal(result.ok, true, JSON.stringify(result.error))
  assert.equal(result.outcome, 'AlreadyStarted')
  assert.deepEqual(result.trace, ['ResolveState'])
  assert.equal(result.acceptCount, 0)
  assert.equal(result.acquireCount, 0)
  assertNoLaterEffects(result)
})

test.todo('WHAT[managed-chat-execution-003] actual chat.message hook never invokes provider after admission rejection or uncertain acceptance (GAP-126)')
