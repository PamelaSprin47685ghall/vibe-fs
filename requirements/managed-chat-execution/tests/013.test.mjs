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

test('WHAT[managed-chat-execution-013] queue full and cancellation cross no bind Host or provider boundary', async () => {
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
      assert.equal(result.hostCount, 0)
    assert.equal(result.commitCount, 0)
    assert.equal(result.releaseCount, 0)
    assert.equal(result.providerCount, 0)
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: fs } = await import("node:fs");
const { fold } = await import("../../../dist/Execution/Session/ChatExecution/Surface.js");
const { createCounters, queryReliability } = await import("../../../dist/OpenCode/Host/ReliabilityDiagnosticsSurface.js");

const acceptedFact = fs.readFileSync(new URL('./fixtures/chat-execution-v1.json', import.meta.url), 'utf8')

test('WHAT[managed-chat-execution-013] diagnostic query derives nonterminal and physical-attempt counts from canonical projection', () => {
  const projected = fold([acceptedFact])
  assert.equal(projected.ok, true)

  const result = queryReliability(
    createCounters(),
    projected.value,
    { waiters: [], activeCount: 0, counters: { duplicate: 0, stale: 0, conflict: 0 } },
    { resumes: [], requeues: [], manualInterventions: [] },
  )

  assert.deepEqual(result.execution, {
    acceptedWithoutTerminal: 1,
    providerStartedWithoutTerminal: 0,
    physicalAttemptsByLogicalRun: [{ logicalRunId: 'run-chat-fixture', physicalAttempts: 1 }],
  })
  assert.equal(Object.isFrozen(result.execution), true)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Runtime = await import("../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const chatExecution = await import("../../../dist/Execution/Session/ChatExecution/Surface.js");
const recoveryHost = await import("../../../dist/OpenCode/Host/SessionRecoveryHostSurface.js");

const withRecoveryHost = async (label, portOutcome, action) => {
  const directory = mkdtempSync(join(tmpdir(), `wxs-chat-recovery-${label}-`))
  const host = await recoveryHost.bootRecoveryHost(directory, portOutcome)

  try {
    await action(host)
  } finally {
    recoveryHost.disposeRecoveryHost(host)
    rmSync(directory, { recursive: true, force: true })
  }
}
const sessionOf = (suffix) => `ses-recovery-${suffix}`
const physicalOf = (suffix) => `msg-recovery-${suffix}`
const assertManualDisposition = (manual, sessionId, physicalId) => {
  assert.equal(manual.reason, 'NoAuthorizedProviderDisposition')
  assert.equal(manual.observation, 'ProviderAbsent')
  assert.equal(manual.lifecycle, 'Accepted')
  assert.equal(manual.sessionId, sessionId)
  assert.equal(manual.physicalUserMessageId, physicalId)
}
const tagged = (name, value) => [name, value]
const plainEvidence = (sessionId, physicalUserMessageId) => ({
  sessionId,
  physicalUserMessageId,
  logicalRunId: `run-${sessionId}`,
  authorityRootUserMessageId: `root-${sessionId}`,
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
  origin: 'HumanRoot',
})
const evidenceWire = (evidence) => ({
  SessionId: tagged('SessionId', evidence.sessionId),
  LogicalRunId: tagged('LogicalRunId', evidence.logicalRunId),
  AuthorityRootUserMessageId: tagged('AuthorityRootUserMessageId', evidence.authorityRootUserMessageId),
  AuthorityKind: evidence.authorityKind,
  IdentitySeed: [
    'RootSelection',
    {
      InitialTier: 'deep',
      Origin: 'ResolvedAtRoot',
      Persona: evidence.identitySeed.participantIdentity.persona,
      PersonaCatalogVersion: evidence.identitySeed.participantIdentity.personaCatalogVersion,
      Role: evidence.identitySeed.participantIdentity.canonicalRole,
      SelectedAgent: evidence.identitySeed.participantIdentity.selectedAgent,
    },
  ],
  PhysicalUserMessageId: tagged('PhysicalUserMessageId', evidence.physicalUserMessageId),
  Origin: ['AuthorityRoot', evidence.origin],
})
const acceptedWire = (evidence) =>
  JSON.stringify([
    'Agent',
    [
      'ChatExecution',
      [
        'Accepted',
        {
          SchemaVersion: 1,
          Key: {
            SessionId: tagged('SessionId', evidence.sessionId),
            PhysicalUserMessageId: tagged('PhysicalUserMessageId', evidence.physicalUserMessageId),
          },
          Evidence: evidenceWire(evidence),
        },
      ],
    ],
  ])

test('WHAT[managed-chat-execution-013] exact terminal settlement revokes the manual', async () => {
  await withRecoveryHost('terminal', 'absent', async (host) => {
    const sessionId = sessionOf('terminal')
    const physicalId = physicalOf('terminal')
    const providerRun = 'provider-recovery-terminal'

    await recoveryHost.seedProviderStarted(host, sessionId, physicalId, providerRun)

    const before = await recoveryHost.resumeAccepted(host, sessionId, physicalId)
    assert.equal(before.manuals.length, 1)

    const settled = await recoveryHost.finalizeCompleted(host, sessionId, physicalId, providerRun)

    assert.deepEqual(settled.manuals, [])
    assert.equal(settled.lifecycle, 'Terminal')
    assert.equal(settled.disposition, 'Completed')
    assert.equal(settled.sessionId, sessionId)
    assert.equal(settled.physicalUserMessageId, physicalId)
  })
})
test('WHAT[managed-chat-execution-013] pre-provider cancellation settlement revokes the manual', async () => {
  await withRecoveryHost('accepted-cancel', 'absent', async (host) => {
    const sessionId = sessionOf('accepted-cancel')
    const physicalId = physicalOf('accepted-cancel')

    await recoveryHost.seedAccepted(host, sessionId, physicalId)

    const before = await recoveryHost.resumeAccepted(host, sessionId, physicalId)
    assert.equal(before.manuals.length, 1)

    const settled = await recoveryHost.signalCancelled(host, sessionId, physicalId)

    assert.deepEqual(settled.manuals, [])
    assert.equal(settled.lifecycle, 'Terminal')
    assert.equal(settled.disposition, 'Cancelled')
    assert.equal(settled.sessionId, sessionId)
    assert.equal(settled.physicalUserMessageId, physicalId)
  })
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { join } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { default: test } = await import("node:test");

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const bootstrap = readFileSync(join(ROOT, 'src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs'), 'utf8')

// WHAT[managed-chat-execution-013] concurrent admissions of one exact key merge into a single flight
// Structure evidence only: the in-flight table lives inside a private HostSignalBootstrap binding and
// no JS surface can deliver two concurrent chat.message admissions, so this asserts the structural
// contract (registration, guarded deregistration, no capacity waiting under lock), not runtime
// concurrency. The durable-accept idempotence and ModelRouting lock/queue guarantees remain the
// underlying behavior proof; this guards the orchestration-layer coalescing shape.
test('WHAT[managed-chat-execution-013] concurrent admissions of one exact key merge into a single in-process flight', () => {
  assert.match(
    bootstrap,
    /let admissionInFlight =\n\s*Dictionary<ChatExecutionKey, Task<Result<ChatAdmissionTransactionOutcome, ChatAdmissionTransactionError>>>\(\)/,
    'the in-process admission flight table must exist',
  )
  assert.match(
    bootstrap,
    /lock admissionInFlight \(fun \(\) ->\n\s*match admissionInFlight\.TryGetValue key with\n\s*\| true, existing -> existing/,
    'a same-key second admission must reuse the registered flight',
  )
  assert.match(
    bootstrap,
    /admissionInFlight\.\[key\] <- started/,
    'the owner must register its started flight under the exact key',
  )
  assert.match(
    bootstrap,
    /finally\n\s*lock admissionInFlight \(fun \(\) ->\n\s*match admissionInFlight\.TryGetValue key with\n\s*\| true, registered when obj\.ReferenceEquals\(registered, flight\) ->\n\s*admissionInFlight\.Remove\(key\) \|> ignore/,
    'deregistration must be guarded by flight identity so a late owner cannot remove a newer flight',
  )

  const flightStart = bootstrap.indexOf('let flight: Task<Result<ChatAdmissionTransactionOutcome, ChatAdmissionTransactionError>> =')
  const flightEnd = bootstrap.indexOf('match! flight with')
  assert.ok(flightStart >= 0 && flightEnd > flightStart, 'flight registration span must exist before the await')
  const flightSpan = bootstrap.slice(flightStart, flightEnd)
  assert.doesNotMatch(flightSpan, /let!|do!|await/, 'no capacity waiting may happen while the lock is held')
})
}
)
}
