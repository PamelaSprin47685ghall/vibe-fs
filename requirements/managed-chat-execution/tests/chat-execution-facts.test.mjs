import assert from 'node:assert/strict'
import test from 'node:test'

import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'
import * as recovery from '../../../dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js'
import * as status from '../../../dist/Execution/Session/ChatExecution/StatusSurface.js'
import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

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
        InitialTier: 'Fast',
        Origin: 'ResolvedAtRoot',
        PeerAgent: 'deep-coder',
        Persona: 'Coder',
        PersonaCatalogVersion: 1,
        Role: 'coder',
        SelectedAgent: 'fast-coder',
      },
    ],
    PhysicalUserMessageId: tagged('PhysicalUserMessageId', physicalUserMessageId),
    Origin: ['AuthorityRoot', 'HumanRoot'],
    EffectiveAgent: 'fast-coder',
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

test('WHAT[CHATEXEC-001] exact key indexes two physical messages within one session', () => {
  const projection = mustFold([
    acceptedWire('msg-a'),
    startedWire('msg-a'),
    terminalWire('msg-a', 'Completed'),
    acceptedWire('msg-b'),
    startedWire('msg-b'),
  ])

  assert.equal(projection.length, 2)
  assert.deepEqual(
    projection.map(({ sessionId, physicalUserMessageId }) => ({ sessionId, physicalUserMessageId })),
    [
      { sessionId: 'ses-chat', physicalUserMessageId: 'msg-a' },
      { sessionId: 'ses-chat', physicalUserMessageId: 'msg-b' },
    ],
  )
  assert.deepEqual(
    { phase: phaseOf(projection, 'msg-a').phase, disposition: phaseOf(projection, 'msg-a').disposition },
    { phase: 'Terminal', disposition: 'Completed' },
  )
  assert.deepEqual(
    { phase: phaseOf(projection, 'msg-b').phase, disposition: phaseOf(projection, 'msg-b').disposition },
    { phase: 'ProviderStarted', disposition: null },
  )
})

test('WHAT[CHATEXEC-004] identical Accepted replay is idempotent and conflicting evidence fails closed', () => {
  const accepted = acceptedWire('msg-replay')
  assert.deepEqual(mustFold([accepted, accepted]), mustFold([accepted]))
  assert.deepEqual(
    mustFold([accepted, startedWire('msg-replay'), accepted]),
    mustFold([accepted, startedWire('msg-replay')]),
  )

  const conflict = acceptedWire('msg-replay', { Evidence: { EffectiveAgent: 'deep-coder' } })
  const rejected = fold([accepted, conflict])
  assert.equal(rejected.ok, false)
  assert.notEqual(rejected.error, '')
})

test('WHAT[CHATEXEC-005] ProviderStarted enforces acceptance provider run and terminal fences', () => {
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

test('WHAT[CHATEXEC-006] same key terminal conflict', () => {
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

test('WHAT[CHATEXEC-006] Terminal directly after Accepted is rejected', () => {
  const result = fold([
    acceptedWire('msg-pre-provider'),
    preProviderTerminalWire('msg-pre-provider', 'Completed'),
  ])

  assert.equal(result.ok, false)
})

test('WHAT[CHATEXEC-007] pre-provider failure cancellation and rejection settle without a provider run', () => {
  for (const disposition of ['Cancelled', 'Rejected', 'Failed']) {
    const messageId = `msg-pre-provider-${disposition.toLowerCase()}`
    const projection = mustFold([
      acceptedWire(messageId),
      preProviderTerminalWire(messageId, disposition),
    ])

    assert.deepEqual(
      { phase: projection[0].phase, disposition: projection[0].disposition },
      { phase: 'Terminal', disposition },
    )
  }
})

test('WHAT[CHATEXEC-010] cancel and delete settle every exact projected execution before capacity is drained', async () => {
  const targetFor = (effectiveAgent) => ({ model: `provider/${effectiveAgent}`, reasoning: 'none' })
  const signals = new Set(recovery.lifecycleSignals())

  for (const [lifecycle, signal] of [
    ['cancel', 'SessionCancelled'],
    ['delete', 'SessionDeleted'],
  ]) {
    assert.equal(signals.has(signal), true)
    const sessionId = `ses-${lifecycle}-drain`
    const messageIds = [`msg-${lifecycle}-a`, `msg-${lifecycle}-b`]
    const effectiveAgents = ['fast-coder', 'deep-coder']
    const runtimes = new Map()
    const facts = []

    for (const [index, physicalUserMessageId] of messageIds.entries()) {
      const effectiveAgent = effectiveAgents[index]
      const exact = { sessionId, physicalUserMessageId, effectiveAgent, target: targetFor(effectiveAgent) }
      const runtime = routing.createRuntime((agent) => targetFor(agent))
      const acquired = await routing.acquireExecutionAdmission(
        runtime,
        sessionId,
        physicalUserMessageId,
        exact.effectiveAgent,
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

test('WHAT[CHATEXEC-002] online prefix integration equals replay from the same canonical facts', () => {
  const history = [
    acceptedWire('msg-online-a'),
    startedWire('msg-online-a'),
    terminalWire('msg-online-a', 'Completed'),
    acceptedWire('msg-online-b'),
    startedWire('msg-online-b'),
    terminalWire('msg-online-b', 'Cancelled'),
  ]
  let online
  for (let length = 1; length <= history.length; length += 1) {
    online = mustFold(history.slice(0, length))
  }

  assert.deepEqual(online, mustFold(history))
})
