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

test('WHAT[managed-chat-execution-006] terminal replay performs no acceptance or capacity effect', async () => {
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

test('WHAT[managed-chat-execution-006] same key terminal conflict', () => {
  const accepted = acceptedWire('msg-terminal')
  const started = startedWire('msg-terminal')
  const completed = terminalWire('msg-terminal', 'Completed')
  assert.deepEqual(
    mustFold([accepted, started, completed, completed]),
    mustFold([accepted, started, completed]),
  )

  const conflict = fold([accepted, started, completed, terminalWire('msg-terminal', 'Failed')])
  assert.equal(conflict.ok, false)
  assert.notEqual(conflict.error, '')
})
test('WHAT[managed-chat-execution-006] Terminal directly after Accepted is rejected', () => {
  const result = fold([
    acceptedWire('msg-pre-provider'),
    preProviderTerminalWire('msg-pre-provider', 'Completed'),
  ])

  assert.equal(result.ok, false)
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

test('WHAT[managed-chat-execution-006] each terminal disposition is durable after provider start', async () => {
  for (const disposition of ['Completed', 'Cancelled', 'Rejected', 'Failed']) {
    const result = await run(accept(), start(), terminal(disposition))

    assert.equal(result.ok, true, JSON.stringify(result.error))
    assert.deepEqual(result.projection, {
      sessionId: evidence().sessionId,
      physicalUserMessageId: evidence().physicalUserMessageId,
      phase: 'Terminal',
      disposition,
    })
    assert.deepEqual(result.appendCounts, { accepted: 1, providerStarted: 1, terminal: 1 })
  }
})
test('WHAT[managed-chat-execution-006] provider terminal before ProviderStarted rejects', async () => {
  const result = await run(accept(), terminal('Completed'))
  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'ProviderNotStarted')
})
test('WHAT[managed-chat-execution-006] conflicting terminal rejects without a second write', async () => {
  const result = await run(
    accept(),
    start(),
    terminal('Completed'),
    terminal('Failed'),
  )

  assert.equal(result.ok, false)
  assert.equal(result.error.kind, 'TerminalConflict')
  assert.deepEqual(result.appendCounts, { accepted: 1, providerStarted: 1, terminal: 1 })
  assert.equal(result.projection.disposition, 'Completed')
})
test('WHAT[managed-chat-execution-006] each uncertain Terminal append leaves projection provider-started', async () => {
  for (const outcome of ['NotAttempted', 'CommitUnknown']) {
    const result = await run(accept(), start(), terminal('Completed', evidence(), outcome))
    assert.equal(result.ok, false)
    assert.equal(result.error.kind, outcome)
    assert.equal(result.projection.phase, 'ProviderStarted')
  }
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

test('WHAT[managed-chat-execution-006] production Host terminal owner persists before exact capacity settlement', () => {
  assert.match(adapterSource, /onExactAssistantObservation[\s\S]*?tryDecodeExactProviderStart[\s\S]*?tryDecodeExactProviderTerminal/)
  assert.match(bootstrapSource, /let\s+startedEvidenceForTerminal[\s\S]*?exactStarted key/)
  assert.match(bootstrapSource, /let\s+applyObservedTerminal[\s\S]*?ExactAssistantTerminal[\s\S]*?NotifyProjectionChanged/)
  assert.match(bootstrapSource, /let\s+settleExactTerminal[\s\S]*?match observation\.Outcome, observation\.Disposition, startedEvidenceForTerminal observation with[\s\S]*?HostProviderTerminalOutcome\.ProviderFailure failure, None, _[\s\S]*?ReconcileWake\.FailureWake\([\s\S]*?Some observation\.PhysicalUserMessageId/)
  assert.match(bootstrapSource, /onExactAssistantObservation\s*=\s*\(fun[\s\S]*?\(started: ExactProviderStartObservation\)[\s\S]*?\(terminal: ExactProviderTerminalObservation option\)[\s\S]*?persistProviderStartedFromObservation[\s\S]*?continueProviderStart started providerStepEnded terminal providerStarted/)

  assert.match(recoveryRuntimeSource, /let recover[\s\S]*?ChatExecutionRecovery\.decide evidence[\s\S]*?interpret ports decision/)
  assert.match(recoveryHostSource, /ExactAssistantTerminal\(started, disposition\)[\s\S]*?ProviderPhysicalObservation\.ProviderTerminal\(started, disposition\)/)
  assert.match(recoveryHostSource, /let finalize[\s\S]*?persistTerminal request\.ExecutionKey request\.TerminalEvidence request\.TerminalDisposition/)
  assert.match(recoveryHostSource, /let persistTerminal[\s\S]*?ManagedChatProviderLifecycle\.terminal journal key started disposition[\s\S]*?requirePersistence "terminal" result[\s\S]*?do! release key/)
  assert.match(recoveryHostSource, /let release[\s\S]*?ModelRouting\.releasePhysicalExecution key\.SessionId key\.PhysicalUserMessageId/)
  assert.match(codecSource, /ProviderRunIdentity/)
})
test('WHAT[managed-chat-execution-006] exact successful Host terminals retain typed finish outcomes', () => {
  for (const [finish, outcome] of [
    ['stop', 'Stop'],
    ['length', 'Length'],
    ['content-filter', 'ContentFiltered'],
  ]) {
    assert.deepEqual(hostSignals.tryDecodeExactProviderTerminal(terminal({ finish })), {
      sessionId: 'ses-terminal',
      physicalUserMessageId: 'msg-terminal',
      providerRun: 'run-terminal',
      outcome,
      failure: '',
      disposition: 'Completed',
    })
  }
})
test('WHAT[managed-chat-execution-006] exact cancel and interruption become closed typed terminal dispositions', () => {
  assert.deepEqual(hostSignals.tryDecodeExactProviderTerminal(terminal({ error: { name: 'AbortError' } })), {
    sessionId: 'ses-terminal',
    physicalUserMessageId: 'msg-terminal',
    providerRun: 'run-terminal',
    outcome: 'Cancelled',
    failure: 'UserCancelled',
    disposition: 'Cancelled',
  })

  assert.deepEqual(hostSignals.tryDecodeExactProviderTerminal(terminal({ error: { name: 'StreamInterruptedError' } })), {
    sessionId: 'ses-terminal',
    physicalUserMessageId: 'msg-terminal',
    providerRun: 'run-terminal',
    outcome: 'ProviderFailure',
    failure: 'ProviderTransient',
    disposition: '',
  })
})
test('WHAT[managed-chat-execution-006] exact provider failure remains typed but awaits retry-owner disposition', () => {
  assert.deepEqual(hostSignals.tryDecodeExactProviderTerminal(terminal({ error: { name: 'TimeoutError', message: 'AbortError' } })), {
    sessionId: 'ses-terminal',
    physicalUserMessageId: 'msg-terminal',
    providerRun: 'run-terminal',
    outcome: 'ProviderFailure',
    failure: 'ProviderTransient',
    disposition: '',
  })
})
test('WHAT[managed-chat-execution-006] ambiguous and deleted evidence fail closed', () => {
  assert.equal(hostSignals.tryDecodeExactProviderTerminal(terminal({ providerRun: '' })), null)
  assert.equal(hostSignals.tryDecodeExactProviderTerminal({ type: 'session.deleted', properties: { sessionID: 'ses-terminal' } }), null)
  assert.match(bootstrapSource, /match observation\.Outcome, observation\.Disposition, startedEvidenceForTerminal observation with/)
  assert.match(bootstrapSource, /\| _ ->[\s\S]*?rejectProviderTerminal observation/)
  assert.match(recoveryHostSource, /eventKey[\s\S]*?ExactAssistantTerminal\(started, _\)[\s\S]*?keyOfStarted started/)
})
}
