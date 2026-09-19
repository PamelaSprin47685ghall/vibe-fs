import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const transaction = await import("../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js");

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
      selectedAgent: 'engineer',
      canonicalRole: 'engineer',
      selectedTier: 'deep',
      persona: 'Engineer',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  },
  providerRun: 'provider-transaction',
  origin: 'HumanRoot',
  requestKind: 'work-main',
  projectionChoice: { kind: 'UseCommittedEpoch' },
}
const run = (failurePoint = 'None', state = 'None') =>
  transaction.transactionScenario(evidence, failurePoint, state)

test('WHAT[managed-chat-execution-003] managed admission has one fixed success order', async () => {
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
test('WHAT[managed-chat-execution-003] append failure performs zero downstream effects', async () => {
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
test('WHAT[managed-chat-execution-003] acquisition failure crosses no later boundary', async () => {
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
test('WHAT[managed-chat-execution-003] superseded demand is a typed nonfatal short-circuit', async () => {
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
test('WHAT[managed-chat-execution-003] already-started replay performs no duplicate admission effect', async () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync } = await import("node:fs");
const { join } = await import("node:path");
const { fileURLToPath } = await import("node:url");

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const bootstrap = readFileSync(join(ROOT, 'src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs'), 'utf8')
const occurrences = (pattern) => bootstrap.match(pattern)?.length ?? 0

test('WHAT[managed-chat-execution-003] managed path calls one admission transaction', () => {
  assert.equal(occurrences(/ChatAdmissionTransaction\.production/g), 1)
  assert.equal(occurrences(/ChatAdmissionTransaction\.execute/g), 1)
  assert.match(
    bootstrap,
    /let\s+admissionTransaction\s*=\s*\n\s*journal\s*\n\s*\|> Option\.map\s*\(fun durable ->\s*\n\s*let runtime = PromptDispatcher\.forPrompts \(PromptJournalAdapter\.create durable\)\s*\n\s*ChatAdmissionTransaction\.production durable runtime\.AcceptManagedChatIntent\)/,
  )
  assert.match(
    bootstrap,
    /let ports = createTransaction \(ModelRouting\.projectHostModel output\)[\s\S]*?ChatAdmissionTransaction\.execute\s*\n\s*ports/,
  )

  const construction = bootstrap.indexOf('ChatAdmissionTransaction.production')
  const hook = bootstrap.indexOf('let chatMessageHook')
  assert.ok(construction >= 0 && construction < hook, 'the transaction factory must be composed before the callback')
})
test('WHAT[managed-chat-execution-003] only Settled crosses the managed provider boundary', () => {
  assert.equal(occurrences(/continueManagedChatMessage/g), 2, 'one declaration and one invocation')
  const admission = bootstrap.slice(
    bootstrap.indexOf('let admitManagedChatMessage'),
    bootstrap.indexOf('let rejectedChatMessage'),
  )

  assert.match(admission, /Ok\(ChatAdmissionTransactionOutcome\.Settled _\) ->\s+continueManagedChatMessage intent output/)
  assert.match(admission, /Ok outcome ->\s+raise \(ChatAdmissionHookException\(TransactionStopped outcome, executionKey intent\)\)/)
  assert.doesNotMatch(admission, /ChatAdmissionTransactionOutcome\.Superseded _\) -> \(\)/)
  assert.equal(occurrences(/TransactionStopped outcome/g), 1)
})
test('WHAT[managed-chat-execution-003] acceptance uncertainty, acquire, bind, and Host projection failures stop before provider', () => {
  const continuation = bootstrap.slice(
    bootstrap.indexOf('let continueManagedChatMessage'),
    bootstrap.indexOf('let currentExecution'),
  )
  const admission = bootstrap.slice(
    bootstrap.indexOf('let admitManagedChatMessage'),
    bootstrap.indexOf('let rejectedChatMessage'),
  )

  assert.match(
    admission,
    /Error error ->\s+raise \(ChatAdmissionHookException\(TransactionFailed error, executionKey intent\)\)/,
  )
  assert.doesNotMatch(continuation, /ChatAdmissionTransaction|TransactionFailed|Error error/)
  assert.doesNotMatch(admission, /Error error[\s\S]*continueManagedChatMessage/)
})
test('WHAT[managed-chat-execution-003] unmanaged and HostInternal preserve the physical continuation without admission', () => {
  assert.match(
    bootstrap,
    /Decision\.NoManagedExecution _[\s\S]*?Decision\.HostInternal _[\s\S]*?continueUnmanagedChatMessage intent/,
  )
})
test('WHAT[managed-chat-execution-003] Reject remains typed at the Host hook boundary', () => {
  assert.match(
    bootstrap,
    /Decision\.Reject rejection, _, _ ->\s*rejectedChatMessage \(IntentRejected rejection\)/,
  )
})
test('WHAT[managed-chat-execution-003] bootstrap contains no fragmented admission owner', () => {
  for (const forbidden of [
    /PromptIngress\.createDecisionHook/,
    /PromptIngress\.createHook/,
    /ModelRouting\.routeChatExecution/,
    /ModelRouting\.AcquireAndCommitRoutedExecution/,
    /SessionExecutionBinding\.acceptRoutedExecution/,
    /SessionExecutionBinding\.acceptExternalExecution/,
    /SessionExecutionBinding\.acceptPromptExecution/,
    /ModelRouting\.projectRoutedModel/,
  ]) {
    assert.doesNotMatch(bootstrap, forbidden)
  }
})
}
