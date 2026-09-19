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

test('WHAT[managed-chat-execution-010] cancel and delete settle every exact projected execution before capacity is drained', async () => {
  const targetFor = (role) => ({ model: `provider/${role}`, reasoning: 'none' })
  const signals = new Set(recovery.lifecycleSignals())

  for (const [lifecycle, signal] of [
    ['cancel', 'SessionCancelled'],
    ['delete', 'SessionDeleted'],
  ]) {
    assert.equal(signals.has(signal), true)
    const sessionId = `ses-${lifecycle}-drain`
    const messageIds = [`msg-${lifecycle}-a`, `msg-${lifecycle}-b`]
    const identities = [
      { role: 'engineer', participant: 'engineer' },
      { role: 'devops', participant: 'devops' },
    ]
    const runtimes = new Map()
    const facts = []

    for (const [index, physicalUserMessageId] of messageIds.entries()) {
      const { role, participant } = identities[index]
      const exact = { sessionId, physicalUserMessageId, role, participant, target: targetFor(role) }
      const runtime = routing.createRuntime((scheduledRole) => targetFor(scheduledRole))
      const acquired = await routing.acquireExecutionAdmission(
        runtime,
        sessionId,
        physicalUserMessageId,
        exact.role,
        exact.participant,
      )
      assert.equal(acquired.kind, 'Acquired')
      assert.deepEqual(routing.commitExecutionAdmission(runtime, acquired.lease, exact), { kind: 'Applied' })
      runtimes.set(physicalUserMessageId, runtime)
      facts.push(
        canonical(acceptedWire(physicalUserMessageId, { SessionId: sessionId })),
        canonical(startedWire(physicalUserMessageId, undefined, sessionId)),
      )
    }

    const projected = chatExecution.nonTerminal(facts, sessionId)
    assert.equal(projected.ok, true, projected.error)
    assert.deepEqual(
      projected.value.map(({ physicalUserMessageId }) => physicalUserMessageId),
      messageIds,
    )

    for (const [index, physicalUserMessageId] of messageIds.entries()) {
      facts.push(canonical(terminalWire(physicalUserMessageId, 'Cancelled', sessionId)))
      assert.deepEqual(
        status.queryFacts(facts, sessionId, physicalUserMessageId).status,
        { accepted: true, providerStarted: true, terminal: true, disposition: 'Cancelled' },
      )
      const runtime = runtimes.get(physicalUserMessageId)
      assert.deepEqual(routing.releasePhysicalExecution(runtime, sessionId, physicalUserMessageId), {
        kind: 'Applied',
      })

      const remaining = chatExecution.nonTerminal(facts, sessionId)
      assert.equal(remaining.ok, true, remaining.error)
      assert.equal(remaining.value.length, messageIds.length - index - 1)
      assert.equal(
        [...runtimes.values()].reduce(
          (active, owner) => active + routing.capacitySnapshot(owner).ledgerEntries.length,
          0,
        ),
        messageIds.length - index - 1,
      )
    }

    assert.deepEqual(chatExecution.nonTerminal(facts, sessionId).value, [])
    for (const owner of runtimes.values()) {
      const drained = routing.capacitySnapshot(owner)
      assert.equal(drained.activeCount, 0)
      assert.deepEqual(drained.executions, [])
      assert.deepEqual(routing.reconcileCapacityEvidence(drained), { kind: 'NoOp' })
    }
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

test('WHAT[managed-chat-execution-010] recovery drain completion uses the Fable-compatible completion owner', () => {
  assert.match(recoveryHostSource, /AsyncSupport\.trySetResult completion \(\)/)
  assert.doesNotMatch(recoveryHostSource, /completion\.TrySetResult/)
})
}
