import assert from 'node:assert/strict'

import * as blog from '../../../../dist/Enforcer/BlogSurface.js'
import * as runtime from '../../../../dist/Context/Companion/RuntimeSurface.js'
import * as routing from '../../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import * as chat from '../../../../dist/Execution/Session/ChatExecution/Surface.js'
import * as persona from '../../../../dist/Participant/Persona/Surface.js'
import * as dispatch from '../../../../dist/Interaction/Dispatch/DispatchSurface.js'

export const operations = Object.freeze([
  'A claims flight',
  'A repair in flight',
  'A superseded',
  'B claims flight',
  'A pending release returns',
  'A terminal callback returns',
  'B observes terminal',
])

export const prerequisites = Object.freeze({
  'A claims flight': Object.freeze([]),
  // W7 minimal interleave: A's repair/observer is already in flight when the
  // supersede lands, so the repair observation causally precedes it. (A
  // repair attempt after the supersede release is a different, unowned
  // observation — covered by the stale-callback named tests, not the
  // minimal interleave.)
  'A repair in flight': Object.freeze(['A claims flight']),
  'A superseded': Object.freeze(['A claims flight', 'A repair in flight']),
  'B claims flight': Object.freeze(['A superseded']),
  'A pending release returns': Object.freeze(['A superseded']),
  'A terminal callback returns': Object.freeze(['A repair in flight', 'A superseded']),
  'B observes terminal': Object.freeze(['B claims flight']),
})

const permutationsOf = (values) => {
  if (values.length === 0) return [[]]
  return values.flatMap((value, index) =>
    permutationsOf([...values.slice(0, index), ...values.slice(index + 1)]).map((tail) => [value, ...tail]),
  )
}

export const permutations = Object.freeze(permutationsOf(operations))

const invalidCausalEdge = (schedule) => {
  const completed = new Set()
  for (const operation of schedule) {
    const missing = prerequisites[operation].find((required) => !completed.has(required))
    if (missing) return Object.freeze({ operation, missing })
    completed.add(operation)
  }
  return null
}

export const validPermutations = Object.freeze(
  permutations.filter((schedule) => invalidCausalEdge(schedule) === null),
)

const K = 'ses-blog'
const PHYS = 'msg-phys-flight'
const ROOT = PHYS
const MAIN = 'ses-main-flight'

const flightRequest = (requestId, toml) =>
  runtime.main({
    requestId,
    mainSession: MAIN,
    bloggerSession: K,
    toml,
    previousIngested: 0,
    nextIngested: 1,
    previousCutoff: 0,
    nextCutoff: 1,
    nextDigest: 'digest-flight',
    frameEpoch: 0,
    deltaDigest: 'delta-flight',
    observedEpoch: 0,
  })

const stubPorts = (calls) => ({
  sessionPort: {
    SubscribeTerminal: (...args) => {
      calls.subscribe.push(args)
      return { Dispose: () => {} }
    },
    SubscribeFutureTerminal: (...args) => {
      calls.subscribeFuture.push(args)
      return { Dispose: () => {} }
    },
    SendPrompt: async (sessionId, text, options) => {
      calls.sendPrompt.push({ sessionId, text, options })
      return dispatch.admittedWithReceipt(`accepted-${calls.sendPrompt.length}`)
    },
  },
  rootWorkspace: {
    TryRead: () => {
      calls.rootRead.push([])
      return undefined
    },
  },
  eventPort: {
    SubscribeTerminalListener: (...args) => {
      calls.eventSubscribe.push(args)
      return { Dispose: () => {} }
    },
    SubscribeFutureTerminalListener: (...args) => {
      calls.eventFuture.push(args)
      return { Dispose: () => {} }
    },
    NotifyTerminal: (...args) => {
      calls.eventNotify.push(args)
      return false
    },
  },
})

const idleObservation = (ports, run) => ({
  quiescent: true,
  context: {
    sessionId: K,
    physicalUserMessageId: PHYS,
    authorityRoot: ROOT,
    providerRun: run,
  },
  sessionPort: ports.sessionPort,
  rootWorkspace: ports.rootWorkspace,
  eventPort: ports.eventPort,
})

const identityFor = (agent) => {
  const resolved = persona.resolveParticipantIdentityAtRoot(agent)
  assert.equal(resolved.ok, true, resolved.ok ? '' : JSON.stringify(resolved.error))
  return {
    selectedAgent: resolved.identity.name,
    role: resolved.identity.role,
    canonicalRole: resolved.identity.role,
    persona: resolved.identity.persona,
    personaCatalogVersion: resolved.identity.catalogVersion,
    origin: resolved.identity.origin,
  }
}

const capacityTarget = { model: 'provider/shared', reasoning: 'none' }
const capacityScheduler = () => capacityTarget

const executionEvidence = (sessionId, physicalUserMessageId) => {
  const identity = identityFor('coder')
  return {
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
      participantIdentity: identity,
    },
    providerRun: `provider-${sessionId}`,
    origin: 'HumanRoot',
    role: identity.role,
    participant: identity.selectedAgent,
    requestKind: 'work-main',
    projectionChoice: { kind: 'UseCommittedEpoch' },
  }
}

const acquireCommittedCapacity = async (capacityRuntime, sessionId, physicalUserMessageId, lenderSessionId = null) => {
  const identity = identityFor('coder')
  const acquisition = await routing.acquireExecutionAdmission(
    capacityRuntime,
    sessionId,
    physicalUserMessageId,
    identity.role,
    identity.selectedAgent,
    lenderSessionId,
  )
  assert.equal(acquisition.kind, 'Acquired')
  assert.deepEqual(routing.executionAdmissionTarget(capacityRuntime, acquisition.lease), capacityTarget)
  assert.deepEqual(
    routing.commitExecutionAdmission(capacityRuntime, acquisition.lease, {
      sessionId,
      physicalUserMessageId,
      role: identity.role,
      participant: identity.selectedAgent,
      target: capacityTarget,
    }),
    { kind: 'Applied' },
  )
  return acquisition.lease
}

export const runFlightInterleaving = async (schedule, { dir, opened, durable }) => {
  const invalid = invalidCausalEdge(schedule)
  if (invalid) {
    const error = new Error(`${invalid.operation} requires ${invalid.missing}`)
    error.code = 'INVALID_CAUSAL_ORDER'
    error.operation = invalid.operation
    error.missing = invalid.missing
    throw error
  }

  // A fatal trip anywhere in this interleave is a proof failure: the W7
  // contract keeps stale A callbacks out of the fuse path, so no trip may
  // fire. WANXIANGSHU_NO_FATAL_EXIT converts the kill into an observable
  // console.error record instead of SIGKILL.
  const fatalRecords = []
  const previousError = console.error
  const previousNoFatalExit = process.env.WANXIANGSHU_NO_FATAL_EXIT
  process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
  console.error = (...args) => {
    fatalRecords.push(args.join(' '))
  }

  const calls = {
    sendPrompt: [],
    subscribe: [],
    subscribeFuture: [],
    rootRead: [],
    eventSubscribe: [],
    eventFuture: [],
    eventNotify: [],
  }
  const ports = stubPorts(calls)
  const scope = runtime.createScope()
  const capacityRuntime = routing.createRuntime(capacityScheduler)
  const observations = []

  const requestA = flightRequest('req-a', 'content-a')
  const requestB = flightRequest('req-b', 'content-b')

  const accepted = await dispatch.acceptHumanRoot(opened.journal, K, PHYS, 'blogger')
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))

  const flightOf = () => {
    const flight = runtime.tryGetFlight(scope, K)
    return flight == null ? null : flight.requestId
  }
  const record = (operation, detail) => {
    observations.push(Object.freeze({ operation, flight: flightOf(), ...detail }))
  }

  let repairStarted = false
  let superseded = false
  let claimedB = false
  let bLease = null
  let bDispatches = 0
  let bTerminal = null

  try {
    for (const operation of schedule) {
      switch (operation) {
        case 'A claims flight': {
          assert.equal(runtime.claimCurrentRequest(scope, K, requestA), 'Claimed')
          assert.equal(flightOf(), 'req-a')
          record(operation, { claim: 'Claimed' })
          break
        }
        case 'A repair in flight': {
          const nudge = await blog.observeIdleRepair(scope, durable, requestA, idleObservation(ports, 'run-a1'))
          assert.equal(nudge.outcome, 'NudgeSent')
          repairStarted = true
          assert.equal(flightOf(), 'req-a')
          record(operation, { outcome: nudge.outcome, sends: calls.sendPrompt.length })
          break
        }
        case 'A superseded': {
          assert.equal(repairStarted, true)
          assert.equal(runtime.releaseCurrentRequest(scope, K, 'req-a'), 'Released')
          superseded = true
          assert.equal(flightOf(), null)
          record(operation, { release: 'Released' })
          break
        }
        case 'B claims flight': {
          assert.equal(superseded, true)
          assert.equal(runtime.claimCurrentRequest(scope, K, requestB), 'Claimed')
          claimedB = true
          assert.equal(flightOf(), 'req-b')
          // Settle the cancelled A repair episode before B observes: the
          // production supersede path (abandonEpisode = durable abandon +
          // exact release) never drains, and the dead A episode squats the
          // repair slot until its CE unwinds (see the drain-lag gap test).
          // Draining here keeps this interleave on the flight/capacity/
          // terminal proof; the drain lag itself is pinned separately.
          await runtime.drainRepairEpisodes(scope)
          bLease = await acquireCommittedCapacity(capacityRuntime, K, PHYS)
          const evidence = executionEvidence(K, PHYS)
          const dispatched = await chat.providerLifecycleScenario([
            { kind: 'Accept', evidence, appendOutcome: 'Committed' },
            { kind: 'ProviderStarted', evidence, appendOutcome: 'Committed' },
            { kind: 'ProviderWork' },
          ])
          assert.equal(dispatched.ok, true, JSON.stringify(dispatched.error))
          bDispatches = dispatched.providerWorkCount
          record(operation, { claim: 'Claimed', providerDispatches: bDispatches })
          break
        }
        case 'A pending release returns': {
          const outcome = runtime.releaseCurrentRequest(scope, K, 'req-a')
          if (claimedB) {
            assert.equal(outcome, 'Conflict:req-b')
            assert.equal(flightOf(), 'req-b')
            assert.equal(calls.eventNotify.length, 0)
          } else {
            assert.equal(outcome, 'Missing')
            assert.equal(flightOf(), null)
          }
          record(operation, { release: outcome })
          break
        }
        case 'A terminal callback returns': {
          const idle = await blog.observeIdleRepair(scope, durable, requestA, idleObservation(ports, 'run-a-late'))
          assert.equal(idle.outcome, 'UnownedIdleIgnored')
          const transform = await blog.observeTransformRepair(scope, durable, requestA, 'run-a-late', [])
          assert.equal(transform.outcome, 'SupersededIgnored')
          assert.equal(calls.eventNotify.length, 0)
          if (claimedB) assert.equal(flightOf(), 'req-b')
          record(operation, { idle: idle.outcome, transform: transform.outcome })
          break
        }
        case 'B observes terminal': {
          assert.equal(claimedB, true)
          const evidence = executionEvidence(K, PHYS)
          const terminal = await chat.providerLifecycleScenario([
            { kind: 'Accept', evidence, appendOutcome: 'Committed' },
            { kind: 'ProviderStarted', evidence, appendOutcome: 'Committed' },
            { kind: 'Terminal', disposition: 'Completed', evidence, appendOutcome: 'Committed' },
          ])
          assert.equal(terminal.ok, true, JSON.stringify(terminal.error))
          assert.equal(terminal.providerWorkCount, 0)
          bTerminal = terminal.projection
          assert.deepEqual(bTerminal, {
            sessionId: K,
            physicalUserMessageId: PHYS,
            phase: 'Terminal',
            disposition: 'Completed',
          })
          record(operation, { terminal: bTerminal.disposition })
          break
        }
        default:
          throw new Error(`unknown operation: ${operation}`)
      }
    }

    assert.equal(superseded, true)
    assert.equal(claimedB, true)
    assert.equal(flightOf(), 'req-b')
    assert.equal(calls.eventNotify.length, 0)
    assert.equal(bDispatches, 1)
    assert.deepEqual(bTerminal, {
      sessionId: K,
      physicalUserMessageId: PHYS,
      phase: 'Terminal',
      disposition: 'Completed',
    })

    const admission = routing.admissionSnapshot(capacityRuntime, K, PHYS)
    assert.equal(admission.activeCapacity, routing.snapshotOccupied(capacityRuntime).length)
    assert.equal(admission.providerBinding, 0)

    const committed = executionEvidence(K, PHYS)
    const committedLifecycle = [
      { kind: 'Accept', evidence: committed, appendOutcome: 'Committed' },
      { kind: 'ProviderStarted', evidence: committed, appendOutcome: 'Committed' },
      { kind: 'Terminal', disposition: 'Completed', evidence: committed, appendOutcome: 'Committed' },
    ]
    const replay = await chat.providerLifecycleScenario(committedLifecycle)
    assert.equal(replay.ok, true, JSON.stringify(replay.error))
    assert.deepEqual(replay.projection, bTerminal)
    assert.equal(replay.providerWorkCount, 0)

    const capacity = routing.capacitySnapshot(capacityRuntime)
    assert.deepEqual(routing.reconcileCapacityEvidence(capacity), { kind: 'NoOp' })
    assert.equal(capacity.executions.length, 1)
    assert.equal(capacity.executions[0].sessionId, K)
    assert.equal(capacity.executions[0].physicalUserMessageId, PHYS)

    const release = routing.releasePhysicalExecution(capacityRuntime, K, PHYS)
    assert.deepEqual(release, { kind: 'Applied' })
    assert.equal(routing.executionAdmissionLifecycle(capacityRuntime, bLease), 'Released')
  } finally {
    try {
      runtime.dispose(scope)
    } catch {}
  }

  console.error = previousError
  if (previousNoFatalExit === undefined) delete process.env.WANXIANGSHU_NO_FATAL_EXIT
  else process.env.WANXIANGSHU_NO_FATAL_EXIT = previousNoFatalExit
  assert.deepEqual(fatalRecords, [])

  void dir

  return Object.freeze({
    schedule: Object.freeze([...schedule]),
    observations: Object.freeze(observations),
    flight: flightOf(),
    providerDispatches: bDispatches,
    terminal: bTerminal,
  })
}
