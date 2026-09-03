import assert from 'node:assert/strict'

import * as chat from '../../../../dist/Execution/Session/ChatExecution/Surface.js'
import * as recovery from '../../../../dist/Execution/Session/ChatExecution/RecoverySurface.js'
import * as authority from '../../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as transaction from '../../../../dist/OpenCode/Host/ChatAdmission/TransactionSurface.js'
import * as routing from '../../../../dist/OpenCode/Host/ModelRoutingSurface.js'
import * as persona from '../../../../dist/Participant/Persona/Surface.js'

export const operations = Object.freeze([
  'parent accepted',
  'child dispatched',
  'child returns',
  'parent new prompt',
  'child terminal',
  'capacity release',
])

export const prerequisites = Object.freeze({
  'parent accepted': Object.freeze([]),
  'child dispatched': Object.freeze(['parent accepted']),
  'child returns': Object.freeze(['child dispatched']),
  'parent new prompt': Object.freeze(['child dispatched']),
  'child terminal': Object.freeze(['child dispatched']),
  'capacity release': Object.freeze(['child terminal']),
})

const permutationsOf = (values) => {
  if (values.length === 0) return [[]]
  return values.flatMap((value, index) =>
    permutationsOf([...values.slice(0, index), ...values.slice(index + 1)]).map((tail) => [value, ...tail]),
  )
}

export const permutations = Object.freeze(permutationsOf(operations))

export const invalidCausalEdge = (schedule) => {
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

const hash = (value) => `H(${value})`
const targetFor = (capacity) => ({
  model: `provider/capacity-${capacity}`,
  reasoning: capacity === 2 ? 'none' : 'high',
})

const schedulerFor = (capacity, target) => (_role, running) => {
  const provider = target.model.slice(0, target.model.indexOf('/'))
  const occupied = running.filter(({ model }) => model.startsWith(`${provider}/`)).length
  return occupied < capacity ? target : null
}

const rootSeed = (agent) => {
  const resolved = persona.resolveParticipantIdentityAtRoot(agent)
  assert.equal(resolved.ok, true, resolved.ok ? '' : resolved.error)
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: resolved.identity.name,
      peerAgent: resolved.identity.name,
      canonicalRole: resolved.identity.role,
      selectedTier: 'deep',
      persona: resolved.identity.persona,
      personaCatalogVersion: resolved.identity.catalogVersion,
      origin: resolved.identity.origin,
    },
  }
}

const rootProfile = (scenario, agent) => {
  const result = authority.createAuthorityRoot(
    hash,
    `runtime-${scenario}`,
    `parent-${scenario}`,
    'HumanRoot',
    `parent-root-${scenario}`,
    rootSeed(agent),
  )
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}

const childProfile = (scenario, childAgent, parent) => {
  const issued = authority.issueInheritedIdentitySeed(childAgent, parent)
  assert.equal(issued.ok, true, issued.ok ? '' : issued.error)
  const result = authority.createAuthorityRoot(
    hash,
    `runtime-${scenario}`,
    `child-${scenario}`,
    'AgentOwnerRoot',
    `child-root-${scenario}`,
    issued.value,
  )
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return { profile: result.value, seed: issued.value }
}

const executionEvidence = (profile, physicalUserMessageId, providerRun) => ({
  sessionId: profile.session,
  physicalUserMessageId,
  logicalRunId: profile.logicalRun,
  authorityRootUserMessageId: profile.authorityRoot,
  authorityKind: profile.authorityKind,
  identitySeed: profile.identitySeed,
  providerRun,
  origin: profile.authorityKind,
  effectiveAgent: profile.participantIdentity.selectedAgent,
  requestKind: 'work-main',
  projectionChoice: { kind: 'UseCommittedEpoch' },
})

const lifecycleActions = (evidence, duplicateDelivery, terminal = false, providerWork = true) => {
  const accept = { kind: 'Accept', evidence, appendOutcome: 'Committed' }
  const start = { kind: 'ProviderStarted', evidence, appendOutcome: 'Committed' }
  const actions = duplicateDelivery
    ? [accept, accept, start, start]
    : [accept, start]
  if (providerWork) actions.push({ kind: 'ProviderWork' })
  if (terminal) {
    const completed = { kind: 'Terminal', disposition: 'Completed', evidence, appendOutcome: 'Committed' }
    actions.push(completed)
    if (duplicateDelivery) actions.push(completed)
  }
  return actions
}

const exactCapacityIdentity = (evidence, target) => ({
  sessionId: evidence.sessionId,
  physicalUserMessageId: evidence.physicalUserMessageId,
  effectiveAgent: evidence.effectiveAgent,
  target,
})

const acquireCommitted = async (runtime, evidence, target) => {
  const acquisition = await routing.acquireExecutionAdmission(
    runtime,
    evidence.sessionId,
    evidence.physicalUserMessageId,
    evidence.effectiveAgent,
  )
  assert.equal(acquisition.kind, 'Acquired')
  assert.deepEqual(routing.executionAdmissionTarget(runtime, acquisition.lease), target)
  assert.deepEqual(
    routing.commitExecutionAdmission(runtime, acquisition.lease, exactCapacityIdentity(evidence, target)),
    { kind: 'Applied' },
  )
  return acquisition.lease
}

const canonicalAuthority = (profile) => ({
  session: profile.session,
  logicalRun: profile.logicalRun,
  authorityRoot: profile.authorityRoot,
  participantIdentity: profile.participantIdentity,
})

const assertOwner = (owner, evidence) => {
  assert.equal(owner.sessionId, evidence.sessionId)
  assert.equal(owner.physicalUserMessageId, evidence.physicalUserMessageId)
  assert.equal(owner.effectiveAgent, evidence.effectiveAgent)
}

export const defaultFamily = Object.freeze({
  name: 'manager-coder-single',
  parentAgent: 'manager',
  childAgent: 'coder',
  capacity: 2,
  replay: false,
  duplicateDelivery: false,
  restartAfter: null,
})

export const runInterleaving = async (schedule, family = defaultFamily) => {
  const invalid = invalidCausalEdge(schedule)
  if (invalid) {
    const error = new Error(`${invalid.operation} requires ${invalid.missing}`)
    error.code = 'INVALID_CAUSAL_ORDER'
    error.operation = invalid.operation
    error.missing = invalid.missing
    throw error
  }

  const scenario = `${family.name}-${validPermutations.findIndex((candidate) => candidate.join('|') === schedule.join('|'))}`
  const target = targetFor(family.capacity)
  const scheduler = schedulerFor(family.capacity, target)
  let runtime = routing.createRuntime(scheduler)
  let processGeneration = 0
  let parent
  let child
  let parentProjection = authority.empty
  let childProjection = authority.empty
  let childEvidence
  let childLease
  let parentCurrentEvidence
  let dispatchResult
  let terminalResult
  let childRelease
  let staleChildSettlement
  let restartedBeforeChildRelease = false
  let durableChildActions = []

  const restart = () => {
    const durableParent = structuredClone(parentProjection)
    const durableChild = structuredClone(childProjection)
    assert.deepEqual(authority.recoverActiveIdentity(durableParent).value.participantIdentity, parent.participantIdentity)
    assert.deepEqual(authority.recoverActiveIdentity(durableChild).value.participantIdentity, child.profile.participantIdentity)
    runtime = routing.createRuntime(scheduler)
    parentProjection = durableParent
    childProjection = durableChild
    processGeneration += 1
  }

  for (const operation of schedule) {
    switch (operation) {
      case 'parent accepted': {
        parent = rootProfile(scenario, family.parentAgent)
        parentProjection = authority.registerAuthority(parent, parentProjection)
        assert.deepEqual(parentProjection.activeLogicalRun, parent)
        const parentEvidence = executionEvidence(
          parent,
          `parent-physical-${scenario}`,
          `parent-provider-${scenario}`,
        )
        const admitted = await transaction.transactionScenario(parentEvidence, 'None', 'None')
        assert.equal(admitted.ok, true, JSON.stringify(admitted.error))
        assert.equal(admitted.durableLifecycle, 'Accepted')
        parentCurrentEvidence = parentEvidence
        await acquireCommitted(runtime, parentEvidence, target)
        break
      }
      case 'child dispatched': {
        child = childProfile(scenario, family.childAgent, parent)
        childProjection = authority.registerAuthority(child.profile, childProjection)
        assert.deepEqual(childProjection.activeLogicalRun, child.profile)
        assert.deepEqual(authority.validateInheritedIdentitySeed(parent, child.seed), {
          ok: true,
          value: child.seed.participantIdentity,
          error: null,
        })
        routing.bindCapacityChild(runtime, parent.session, child.profile.session)
        childEvidence = executionEvidence(
          child.profile,
          `child-physical-${scenario}`,
          `child-provider-${scenario}`,
        )
        childLease = await acquireCommitted(runtime, childEvidence, target)
        durableChildActions = lifecycleActions(childEvidence, family.duplicateDelivery)
        dispatchResult = await chat.providerLifecycleScenario(durableChildActions)
        assert.equal(dispatchResult.ok, true, JSON.stringify(dispatchResult.error))
        assert.equal(dispatchResult.providerWorkCount, 1)
        break
      }
      case 'child returns': {
        const serialized = authority.serializeIdentitySeed(child.seed)
        assert.equal(serialized.ok, true, serialized.error)
        const replayed = authority.rehydrateIdentitySeed(serialized.value)
        assert.equal(replayed.ok, true, replayed.error)
        assert.deepEqual(replayed.value, child.seed)
        assert.deepEqual(authority.validateInheritedIdentitySeed(parent, replayed.value).value, child.seed.participantIdentity)
        break
      }
      case 'parent new prompt': {
        const newer = executionEvidence(
          parent,
          `parent-new-physical-${scenario}`,
          `parent-new-provider-${scenario}`,
        )
        const admitted = await transaction.transactionScenario(newer, 'None', 'None')
        assert.equal(admitted.ok, true, JSON.stringify(admitted.error))
        assert.equal(admitted.durableLifecycle, 'Accepted')
        await acquireCommitted(runtime, newer, target)
        parentCurrentEvidence = newer
        assert.deepEqual(canonicalAuthority(parentProjection.activeLogicalRun), canonicalAuthority(parent))
        assert.equal(childEvidence.physicalUserMessageId.startsWith('child-physical-'), true)
        break
      }
      case 'child terminal': {
        durableChildActions = lifecycleActions(childEvidence, family.duplicateDelivery, true, false)
        terminalResult = await chat.providerLifecycleScenario(durableChildActions)
        assert.equal(terminalResult.ok, true, JSON.stringify(terminalResult.error))
        assert.equal(terminalResult.providerWorkCount, 0)
        assert.deepEqual(terminalResult.projection, {
          sessionId: childEvidence.sessionId,
          physicalUserMessageId: childEvidence.physicalUserMessageId,
          phase: 'Terminal',
          disposition: 'Completed',
        })
        break
      }
      case 'capacity release': {
        restartedBeforeChildRelease = processGeneration > 0
        childRelease = routing.releasePhysicalExecution(
          runtime,
          childEvidence.sessionId,
          childEvidence.physicalUserMessageId,
        )
        staleChildSettlement = routing.releaseExecutionAdmissionBeforeProvider(
          runtime,
          childLease,
          exactCapacityIdentity(childEvidence, target),
        )
        break
      }
      default:
        throw new Error(`unknown operation: ${operation}`)
    }

    if (family.restartAfter === operation) restart()
  }

  const canonicalReplay = await chat.providerLifecycleScenario(
    lifecycleActions(childEvidence, family.duplicateDelivery || family.replay, true, false),
  )
  assert.equal(canonicalReplay.ok, true, JSON.stringify(canonicalReplay.error))
  assert.deepEqual(terminalResult.projection, canonicalReplay.projection)
  assert.equal(canonicalReplay.providerWorkCount, 0)
  assert.equal(dispatchResult.providerWorkCount, 1)

  const capacity = routing.capacitySnapshot(runtime)
  assert.deepEqual(routing.reconcileCapacityEvidence(capacity), { kind: 'NoOp' })
  assert.deepEqual(routing.capacitySnapshot(runtime), capacity)
  assert.equal(Object.isFrozen(capacity), true)
  assert.equal(Object.isFrozen(capacity.tokens), true)

  if (!restartedBeforeChildRelease) {
    assert.deepEqual(childRelease, { kind: 'Applied' })
    assert.ok(['Conflict', 'AlreadyApplied', 'StaleFence'].includes(staleChildSettlement.kind))
  } else {
    assert.ok(['AlreadyApplied', 'StaleFence'].includes(childRelease.kind))
    assert.equal(staleChildSettlement.kind, 'StaleFence')
  }

  if (processGeneration === 0) {
    assert.equal(routing.executionAdmissionLifecycle(runtime, childLease), 'Released')
    assert.equal(capacity.executions.length, 1)
  }

  assert.equal(capacity.executions.some((owner) => owner.sessionId === childEvidence.sessionId), false)
  for (const owner of capacity.executions) assertOwner(owner, parentCurrentEvidence)
  for (const owner of capacity.owners) assertOwner(owner, parentCurrentEvidence)
  for (const token of capacity.tokens) assertOwner(token.owner, parentCurrentEvidence)
  for (const custody of capacity.custodies) assertOwner(custody.owner, parentCurrentEvidence)

  assert.ok(
    capacity.lineage.some(
      ({ parentSessionId, childSessionId }) =>
        parentSessionId === parent.session && childSessionId === child.profile.session,
    ) || processGeneration > 0,
  )

  const admission = routing.admissionSnapshot(
    runtime,
    childEvidence.sessionId,
    childEvidence.physicalUserMessageId,
  )
  assert.equal(admission.activeCapacity, routing.snapshotOccupied(runtime).length)
  assert.equal(admission.providerBinding, 0)
  assert.deepEqual(recovery.decideScenario('CrashAfterAcceptance'), {
    kind: 'ResumePreProvider',
    request: 'ResumeAcceptedAdmission',
    disposition: null,
  })

  assert.notEqual(parent.session, child.profile.session)
  assert.notEqual(parent.participantIdentity.selectedAgent, child.profile.participantIdentity.selectedAgent)
  assert.deepEqual(child.seed.participantIdentity, child.profile.participantIdentity)
  assert.equal(child.seed.ownerSession, parent.session)
  assert.equal(child.seed.ownerLogicalRun, parent.logicalRun)
  assert.equal(child.seed.ownerAuthorityRoot, parent.authorityRoot)
  assert.deepEqual(canonicalAuthority(parentProjection.activeLogicalRun), canonicalAuthority(parent))
  assert.deepEqual(canonicalAuthority(childProjection.activeLogicalRun), canonicalAuthority(child.profile))

  return Object.freeze({
    schedule: Object.freeze([...schedule]),
    parent: canonicalAuthority(parent),
    child: canonicalAuthority(child.profile),
    providerDispatches: dispatchResult.providerWorkCount,
    capacity,
    processGeneration,
  })
}
