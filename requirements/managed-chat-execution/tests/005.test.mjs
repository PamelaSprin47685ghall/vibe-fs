import test from 'node:test'

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
