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

test('WHAT[interaction-authority-005] exhaustive chat admission intent table', () => {
  const claim = {
    promptKey: 'prompt-1',
    sessionId: 'ses-chat',
    origin: 'InteractionRepair',
    participant: 'engineer',
  }

  const cases = [
    {
      label: 'unmanaged fresh host message',
      decoded: message(),
      expected: { case: 'NoManagedExecution', reason: 'UnmanagedMessage' },
    },
    {
      label: 'fresh external managed root',
      decoded: message({ explicitAgent: 'engineer' }),
      expected: {
        case: 'ExternalRootIntent',
        sessionId: 'ses-chat',
        physicalUserMessageId: 'msg-chat',
        explicitAgent: 'engineer',
        participant: 'engineer',
        origin: 'HumanRoot',
        identitySeed: 'RootSelection',
      },
    },
    {
      label: 'exact claimed plugin prompt',
      decoded: message({ promptKey: 'prompt-1' }),
      durable: snapshot({ claims: [claim] }),
      expected: {
        case: 'PendingPromptIntent',
        sessionId: 'ses-chat',
        physicalUserMessageId: 'msg-chat',
        promptKey: 'prompt-1',
        participant: 'engineer',
        origin: 'InteractionRepair',
        identitySeed: 'RootSelection',
      },
    },
    {
      label: 'host compaction',
      decoded: message({ hostCompaction: true }),
      expected: { case: 'HostInternal', origin: 'HostInternal' },
    },
    {
      label: 'host synthetic',
      decoded: message({ hostSynthetic: true }),
      expected: { case: 'HostInternal', origin: 'HostInternal' },
    },
  ]

  for (const row of cases) {
    assert.deepEqual(intent.resolve(row.decoded, row.durable ?? snapshot()), row.expected, row.label)
  }
})
test('WHAT[interaction-authority-005] rejects managed intent without physical message identity', () => {
  assert.deepEqual(decide(message({ physicalUserMessageId: null, explicitAgent: 'engineer' })), {
    case: 'Reject',
    reason: 'ManagedIntentMissingPhysicalUserMessageId',
  })

  assert.deepEqual(
    decide(
      message({ physicalUserMessageId: null, promptKey: 'prompt-1' }),
      snapshot({
        claims: [
          {
            promptKey: 'prompt-1',
            sessionId: 'ses-chat',
            origin: 'InteractionRepair',
            participant: 'engineer',
          },
        ],
      }),
    ),
    { case: 'Reject', reason: 'ManagedIntentMissingPhysicalUserMessageId' },
  )
})
test('WHAT[interaction-authority-005] rejects insufficient exact identity evidence', () => {
  const rows = [
    [message({ sessionId: null, explicitAgent: 'engineer' }), 'ManagedIntentMissingSessionId'],
    [message({ explicitAgent: 'legacy-coder' }), 'InvalidExplicitAgent'],
    [message({ promptKey: 'missing' }), 'PromptKeyNotClaimed'],
  ]

  for (const [decoded, reason] of rows) {
    assert.deepEqual(decide(decoded), { case: 'Reject', reason })
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const chatExecution = await import("../../../dist/Execution/Session/ChatExecution/Surface.js");
const recovery = await import("../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js");
const status = await import("../../../dist/Execution/Session/ChatExecution/StatusSurface.js");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");

const tagged = (name, value) => [name, value]
const keyWire = (sessionId, physicalUserMessageId) => ({
  SessionId: tagged('SessionId', sessionId),
  PhysicalUserMessageId: tagged('PhysicalUserMessageId', physicalUserMessageId),
})
const factWire = (factCase, payload) => JSON.stringify(['Agent', ['ChatExecution', [factCase, payload]]])
const acceptedWire = (physicalUserMessageId, overrides = {}) => {
  const sessionId = overrides.SessionId ?? 'ses-chat'
  const evidence = {
    SessionId: tagged('SessionId', sessionId),
    LogicalRunId: tagged('LogicalRunId', `run-${physicalUserMessageId}`),
    AuthorityRootUserMessageId: tagged('AuthorityRootUserMessageId', `root-${physicalUserMessageId}`),
    AuthorityKind: 'HumanRoot',
    IdentitySeed: [
      'RootSelection',
      {
        InitialTier: 'deep',
        Origin: 'ResolvedAtRoot',
        Persona: 'Engineer',
        PersonaCatalogVersion: 1,
        Role: 'engineer',
        SelectedAgent: 'engineer',
      },
    ],
    PhysicalUserMessageId: tagged('PhysicalUserMessageId', physicalUserMessageId),
    Origin: ['AuthorityRoot', 'HumanRoot'],
    ...overrides.Evidence,
  }

  return factWire('Accepted', {
    Evidence: evidence,
    Key: keyWire(sessionId, physicalUserMessageId),
    SchemaVersion: overrides.SchemaVersion ?? 1,
  })
}
const startedWire = (physicalUserMessageId, providerRun = `provider-${physicalUserMessageId}`, sessionId = 'ses-chat') =>
  factWire('ProviderStarted', {
    Evidence: {
      Accepted: JSON.parse(acceptedWire(physicalUserMessageId, { SessionId: sessionId }))[1][1][1].Evidence,
      ProviderRun: tagged('ProviderRunIdentity', providerRun),
      RequestKind: 'WorkMain',
      ProjectionChoice: 'UseCommittedEpoch',
    },
    Key: keyWire(sessionId, physicalUserMessageId),
    SchemaVersion: 1,
  })
const terminalWire = (physicalUserMessageId, disposition, sessionId = 'ses-chat') =>
  factWire('Terminal', {
    Disposition: disposition,
    Evidence: ['AfterProviderStart', {
      Accepted: JSON.parse(acceptedWire(physicalUserMessageId, { SessionId: sessionId }))[1][1][1].Evidence,
      ProviderRun: tagged('ProviderRunIdentity', `provider-${physicalUserMessageId}`),
      RequestKind: 'WorkMain',
      ProjectionChoice: 'UseCommittedEpoch',
    }],
    Key: keyWire(sessionId, physicalUserMessageId),
    SchemaVersion: 1,
  })
const preProviderTerminalWire = (physicalUserMessageId, disposition) =>
  factWire('Terminal', {
    Disposition: disposition,
    Evidence: ['PreProvider', JSON.parse(acceptedWire(physicalUserMessageId))[1][1][1].Evidence],
    Key: keyWire('ses-chat', physicalUserMessageId),
    SchemaVersion: 1,
  })
const canonical = (wire) => {
  const result = chatExecution.canonicalize(wire)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}
const fold = (wires) => chatExecution.fold(wires.map(canonical))
const mustFold = (wires) => {
  const result = fold(wires)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}
const phaseOf = (projection, physicalUserMessageId) =>
  projection.find((entry) => entry.physicalUserMessageId === physicalUserMessageId)

test('WHAT[managed-chat-execution-005] ProviderStarted enforces acceptance provider run and terminal fences', () => {
  const beforeAccepted = fold([startedWire('msg-start')])
  assert.equal(beforeAccepted.ok, false)

  const mismatchedRun = fold([
    acceptedWire('msg-start'),
    startedWire('msg-start'),
    startedWire('msg-start', 'provider-other'),
  ])
  assert.equal(mismatchedRun.ok, false)

  const accepted = acceptedWire('msg-start')
  const started = startedWire('msg-start')
  assert.deepEqual(mustFold([accepted, started, started]), mustFold([accepted, started]))

  const afterTerminal = fold([
    acceptedWire('msg-terminal-start'),
    startedWire('msg-terminal-start'),
    terminalWire('msg-terminal-start', 'Cancelled'),
    startedWire('msg-terminal-start'),
  ])
  assert.equal(afterTerminal.ok, false)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const chatExecution = await import("../../../dist/Execution/Session/ChatExecution/Surface.js");

const evidence = (overrides = {}) => ({
  sessionId: 'ses-provider-lifecycle',
  physicalUserMessageId: 'msg-provider-lifecycle',
  logicalRunId: 'run-provider-lifecycle',
  authorityRootUserMessageId: 'root-provider-lifecycle',
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
  providerRun: 'provider-provider-lifecycle',
  origin: 'HumanRoot',
  requestKind: 'work-main',
  projectionChoice: { kind: 'UseCommittedEpoch' },
  ...overrides,
})
const accept = (attempt = evidence(), appendOutcome = 'Committed') => ({
  kind: 'Accept',
  evidence: attempt,
  appendOutcome,
})
const start = (attempt = evidence(), appendOutcome = 'Committed') => ({
  kind: 'ProviderStarted',
  evidence: attempt,
  appendOutcome,
})
const terminal = (disposition, attempt = evidence(), appendOutcome = 'Committed') => ({
  kind: 'Terminal',
  disposition,
  evidence: attempt,
  appendOutcome,
})
const run = (...actions) => chatExecution.providerLifecycleScenario(actions)

test('WHAT[managed-chat-execution-005] equal start and terminal duplicates are semantic no-ops', async () => {
  const result = await run(
    accept(),
    start(),
    start(),
    terminal('Completed'),
    terminal('Completed'),
  )

  assert.equal(result.ok, true, JSON.stringify(result.error))
  assert.deepEqual(result.appendCounts, { accepted: 1, providerStarted: 1, terminal: 1 })
  assert.equal(result.semanticTransitionCount, 3)
})
test('WHAT[managed-chat-execution-005] ProviderStarted before Accepted rejects', async () => {
  const result = await run(start())
  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'MissingAccepted')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFile } = await import("node:fs/promises");
const { default: test } = await import("node:test");
const hostSignals = await import("../../../dist/OpenCode/Host/HostSignalSurface.js");

const codecSource = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Codec/HostEventCodec.fs', import.meta.url), 'utf8')
const adapterSource = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Signals/HostSignalAdapter.fs', import.meta.url), 'utf8')
const bindingSource = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs', import.meta.url), 'utf8')
const bootstrapSource = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs', import.meta.url), 'utf8')
const recoveryHostSource = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Host/SessionRecoveryHost.fs', import.meta.url), 'utf8')
const recoveryRuntimeSource = await readFile(new URL('../../../src/Wanxiangshu/Execution/Session/ChatExecution/RecoveryRuntime.fs', import.meta.url), 'utf8')
const recoverySource = await readFile(new URL('../../../src/Wanxiangshu/OpenCode/Host/PluginRecoveryScope.fs', import.meta.url), 'utf8')
const terminal = ({
  sessionId = 'ses-terminal',
  physicalUserMessageId = 'msg-terminal',
  providerRun = 'run-terminal',
  finish,
  error,
  completed = 2,
} = {}) => ({
  type: 'message.updated',
  properties: {
    info: {
      sessionID: sessionId,
      id: providerRun,
      role: 'assistant',
      parentID: physicalUserMessageId,
      time: { created: 1, completed },
      ...(finish === undefined ? {} : { finish }),
      ...(error === undefined ? {} : { error }),
    },
  },
})

test('WHAT[managed-chat-execution-005] exact public assistant observation alone establishes provider start', () => {
  const started = terminal({ completed: undefined })
  delete started.properties.info.time.completed
  assert.deepEqual(hostSignals.tryDecodeExactProviderStart(started), {
    sessionId: 'ses-terminal',
    physicalUserMessageId: 'msg-terminal',
    providerRun: 'run-terminal',
  })

  for (const mutation of [
    (event) => { event.properties.info.role = 'user' },
    (event) => { event.properties.info.id = '' },
    (event) => { event.properties.info.parentID = '' },
    (event) => { event.properties.info.sessionID = '' },
    (event) => { delete event.properties.info.time.created },
  ]) {
    const ambiguous = structuredClone(started)
    mutation(ambiguous)
    assert.equal(hostSignals.tryDecodeExactProviderStart(ambiguous), null)
  }

  assert.match(bootstrapSource, /let\s+continueStartedLifecycle[\s\S]*?ModelRouting\.endProviderStep[\s\S]*?settleObservedTerminal/)
  assert.match(bootstrapSource, /let signalNewProviderStart started providerStarted =[\s\S]*?if providerStarted then[\s\S]*?signalProviderStarted started/)
  assert.match(bootstrapSource, /let continueProviderStart[\s\S]*?match persistence with[\s\S]*?\| Error \w+ ->[\s\S]*?rejectProviderStart started[\s\S]*?\| Ok providerStarted ->[\s\S]*?BindPhysicalUserMaterial\(started\.SessionId, started\.PhysicalUserMessageId\)[\s\S]*?signalNewProviderStart started providerStarted[\s\S]*?continueStartedLifecycle/)
  assert.match(bootstrapSource, /persistProviderStartedFromObservation[\s\S]*?continueProviderStart started providerStepEnded terminal providerStarted/)
  assert.match(bindingSource, /persistObservedProviderStart[\s\S]*?ChatExecutionProjection\.byKey key/)
  assert.match(bindingSource, /match execution \|> Option\.map _\.Lifecycle, execution \|> Option\.bind _\.ProviderStarted with[\s\S]*?ChatExecutionLifecycle\.Terminal[\s\S]*?AcceptedExecutionAlreadyTerminal[\s\S]*?\| _, Some _ -> Task\.FromResult\(Ok false\)[\s\S]*?\| _, None ->[\s\S]*?bindAttemptPlan[\s\S]*?do! persistPreparedProviderStarted[\s\S]*?return true/)
  assert.match(bindingSource, /persistObservedProviderStart[\s\S]*?bindAttemptPlan observation\.SessionId observation\.PhysicalUserMessageId observation\.ProviderRun/)
  assert.match(recoverySource, /TryBindAttemptPlan[\s\S]*?established\.Profile\.PhysicalUserMessageId = physicalUserMessageId/)
  assert.match(recoverySource, /\| Some _ -> None[\s\S]*?\| None -> this\.BindPendingAttemptPlan/)
  assert.match(bindingSource, /ManagedChatProviderLifecycle\.providerStarted/)
})
}
