import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const intent = await import("../../../dist/OpenCode/Host/ChatAdmission/IntentSurface.js");

const message = (overrides = {}) => ({
  sessionId: 'ses-chat',
  physicalUserMessageId: 'msg-chat',
  explicitAgent: null,
  promptKey: null,
  hostCompaction: false,
  hostSynthetic: false,
  ...overrides,
})
const snapshot = (overrides = {}) => ({
  available: true,
  activeParticipant: null,
  activeKind: null,
  claims: [],
  acceptedContinuations: [],
  ...overrides,
})
const decide = (decoded, durable = snapshot()) => intent.resolve(decoded, durable)

test('WHAT[INTERACTION-AUTHORITY-008] accepted Host identity outranks claim and compaction', () => {
  const durable = snapshot({
    claims: [
      {
        promptKey: 'prompt-1',
        sessionId: 'ses-chat',
        origin: 'InteractionRepair',
        participant: 'engineer',
      },
    ],
    acceptedContinuations: [{ physicalUserMessageId: 'msg-chat', origin: 'JoinGuard' }],
  })

  assert.deepEqual(
    decide(message({ promptKey: 'prompt-1', hostCompaction: true }), durable),
    { case: 'NoManagedExecution', reason: 'AlreadyAcceptedHostMessage', origin: 'JoinGuard' },
  )
})
test('WHAT[INTERACTION-AUTHORITY-008] registered AgentOwnerRoot outranks external root inference', () => {
  assert.deepEqual(
    decide(
      message({ promptKey: 'unclaimed-owner-root', explicitAgent: 'reviewer' }),
      snapshot({ activeParticipant: 'engineer', activeKind: 'AgentOwnerRoot' }),
    ),
    { case: 'Reject', reason: 'AgentOwnerRootPromptNotClaimed' },
  )
})
test('WHAT[INTERACTION-AUTHORITY-008] plugin claim is frozen even if the later projection changes', () => {
  const durable = snapshot({
    claims: [
      {
        promptKey: 'prompt-1',
        sessionId: 'ses-chat',
        origin: 'InteractionRepair',
        participant: 'engineer',
      },
    ],
  })

  const resolved = decide(message({ promptKey: 'prompt-1' }), durable)
  durable.claims[0].participant = 'reviewer'
  durable.claims.length = 0

  assert.equal(resolved.case, 'PendingPromptIntent')
  assert.equal(resolved.promptKey, 'prompt-1')
  assert.equal(resolved.participant, 'engineer')
  assert.equal('effectiveAgent' in resolved, false, 'intent carries no EffectiveAgent')
  assert.equal('selectedAgent' in resolved, false, 'intent carries no SelectedAgent')
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

test('WHAT[CHATEXEC-008] recovery begins from durable activation and re-enters only on causal events', async () => {
  const [beforeDurability, afterDurability] = (await Runtime.admissionCrashPointScenarios(
    ['A', 'B'],
    'ProcessRestart',
    'NotCommitted',
    'Applied',
  )).scenarios

  assert.deepEqual(beforeDurability.decisions, ['NoDurableExecution', 'NoDurableExecution'])
  assert.deepEqual(beforeDurability.effects, [])
  assert.deepEqual(afterDurability.decisions, ['ResumePreProvider', 'ResumePreProvider'])
  assert.deepEqual(afterDurability.effects, ['ResumePreProvider', 'ResumePreProvider'])
  assert.deepEqual(Runtime.lifecycleSignals(), [
    'DurabilityActivated',
    'PluginRuntimeReloaded',
    'ExactAssistantStarted',
    'ExactAssistantTerminal',
    'SessionAborted',
    'SessionDeleted',
    'SessionCancelled',
    'CapacityProjectionReplayed',
  ])
})
}
