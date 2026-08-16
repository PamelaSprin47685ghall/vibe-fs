import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, listItems, payloadOf } from '../../verification-system/tests/support/domain.mjs'
import { SessionIdModule_create as sessionId, SessionIdModule_value as sessionValue } from '../../../dist/Foundation/Identity.js'

const Fission = await import('../../../dist/Execution/Fission/Model.js')
import {
  FissionAdmissionModule_create as createAdmission,
  FissionAdmissionModule_admit as admit,
  FissionAdmissionModule_isActive as isActive,
  FissionAdmissionModule_release as release,
} from '../../../dist/Execution/Fission/Admission.js'

const parsed = () => payloadOf(Fission.FissionPrompt_parse(' lane A  \nlane B'))

const harness = ({ failCreateAt, failStartAt, failInterrupt = false, parent = 'old-parent' } = {}) => {
  const events = []
  let serial = 0
  const deps = {
    ParentOf: async (owner) => {
      events.push(['parent', sessionValue(owner)])
      return { tag: 0, fields: [parent == null ? undefined : sessionId(parent)] }
    },
    OwnerWorkRecord: async (owner) => {
      events.push(['lwr', sessionValue(owner)])
      return { tag: 0, fields: ['CANONICAL-LWR'] }
    },
    CreateLane: async (_owner, physicalParent, lane) => {
      events.push(['create', lane.Index, physicalParent == null ? null : sessionValue(physicalParent)])
      if (lane.Index === failCreateAt) return { tag: 1, fields: [`create-${lane.Index}`] }
      serial += 1
      return { tag: 0, fields: [sessionId(`lane-${serial}`)] }
    },
    StartLane: async (laneSession, startup) => {
      const index = Number(/lane_index = (\d+)/.exec(startup)?.[1])
      events.push(['start', index, sessionValue(laneSession), startup])
      if (index === failStartAt) return { tag: 1, fields: [`start-${index}`] }
      return { tag: 0, fields: [] }
    },
    AbortLane: async (laneSession) => events.push(['rollback', sessionValue(laneSession)]),
    SilentInterruptOwner: async (owner) => {
      events.push(['silent-interrupt', sessionValue(owner)])
      return failInterrupt ? { tag: 1, fields: ['interrupt-failed'] } : { tag: 0, fields: [] }
    },
  }
  return { events, runtime: createAdmission(deps) }
}

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-003] admission creates fresh sibling sessions with old parent and starts from LWR + exact lane input', async () => {
  const { events, runtime } = harness()
  const owner = sessionId('old-caller')
  const result = await admit(runtime, owner, parsed())
  assert.equal(caseOf(result), 'Ok')

  const creates = events.filter(([kind]) => kind === 'create')
  assert.deepEqual(creates.map(([, index, parent]) => [index, parent]), [[0, 'old-parent'], [1, 'old-parent']])
  const starts = events.filter(([kind]) => kind === 'start')
  assert.equal(starts.length, 2)
  assert.match(starts[0][3], /CANONICAL-LWR/)
  assert.match(starts[0][3], /lane A  /, 'lane input spaces are preserved')
  assert.match(starts[1][3], /lane B/)
  assert.equal(isActive(runtime, owner), true)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-005] old caller silent-interrupts only after every lane started', async () => {
  const { events, runtime } = harness()
  const owner = sessionId('old-caller-interrupt-order')
  const result = await admit(runtime, owner, parsed())
  assert.equal(caseOf(result), 'Ok')

  const interruptAt = events.findIndex(([kind]) => kind === 'silent-interrupt')
  assert.ok(interruptAt > events.findLastIndex(([kind]) => kind === 'start'), 'old caller interrupts only after every lane started')
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-013] user-facing root caller is rejected before fission reserves or creates anything', async () => {
  const { events, runtime } = harness({ parent: null })
  const owner = sessionId('root-caller')
  const result = await admit(runtime, owner, parsed())

  assert.equal(caseOf(result), 'Error')
  assert.deepEqual(events, [['parent', 'root-caller']])
  assert.equal(isActive(runtime, owner), false)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-004] partial create or start failure rolls back every created lane and never interrupts old caller', async () => {
  for (const options of [{ failCreateAt: 1 }, { failStartAt: 1 }]) {
    const { events, runtime } = harness(options)
    const owner = sessionId(`owner-${JSON.stringify(options)}`)
    const result = await admit(runtime, owner, parsed())
    assert.equal(caseOf(result), 'Error')
    assert.equal(events.some(([kind]) => kind === 'silent-interrupt'), false)
    const created = events.filter(([kind]) => kind === 'create' && !events.some(() => false)).length
    const rolledBack = events.filter(([kind]) => kind === 'rollback').length
    assert.ok(rolledBack >= 1)
    assert.equal(isActive(runtime, owner), false)
  }
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-005] failed silent interrupt rolls back lanes and old caller stays out of active set', async () => {
  const failed = harness({ failInterrupt: true })
  const failedOwner = sessionId('interrupt-owner')
  assert.equal(caseOf(await admit(failed.runtime, failedOwner, parsed())), 'Error')
  assert.equal(failed.events.filter(([k]) => k === 'rollback').length, 2)
  assert.equal(isActive(failed.runtime, failedOwner), false)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-011] second admission while active is rejected as AlreadyFissioned until release', async () => {
  const live = harness()
  const owner = sessionId('single-flight-owner')
  assert.equal(caseOf(await admit(live.runtime, owner, parsed())), 'Ok')
  const second = await admit(live.runtime, owner, parsed())
  assert.equal(caseOf(second), 'Error')
  assert.equal(caseOf(payloadOf(second)), 'AlreadyFissioned')
  release(live.runtime, owner)
  assert.equal(isActive(live.runtime, owner), false)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-005] FissionRuntime preserves silent interrupt across multiple checks and is cleared only by clearOwner/clearSilentInterrupt', async () => {
  const {
    FissionRuntime_markSilentInterrupt: markSilentInterrupt,
    FissionRuntime_isSilentInterrupt: isSilentInterrupt,
    FissionRuntime_tryConsumeSilentInterrupt: tryConsumeSilentInterrupt,
    FissionRuntime_clearSilentInterrupt: clearSilentInterrupt,
    FissionRuntime_clearOwner: clearOwner,
  } = await import('../../../dist/Execution/Fission/Runtime.js')

  const owner = sessionId('retired-owner-1')
  assert.equal(isSilentInterrupt(owner), false)

  markSilentInterrupt(owner)
  assert.equal(isSilentInterrupt(owner), true)
  assert.equal(tryConsumeSilentInterrupt(owner), true)
  // Must NOT be cleared after consuming once:
  assert.equal(isSilentInterrupt(owner), true)
  assert.equal(tryConsumeSilentInterrupt(owner), true)

  clearSilentInterrupt(owner)
  assert.equal(isSilentInterrupt(owner), false)

  markSilentInterrupt(owner)
  assert.equal(isSilentInterrupt(owner), true)
  clearOwner(owner)
  assert.equal(isSilentInterrupt(owner), false)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-009] observeLaneTurn and OrdinaryTurnWorkflow absorb Fission-replaced owner turns without sending continuations', async () => {
  const {
    FissionRuntime_markSilentInterrupt: markSilentInterrupt,
    FissionRuntime_clearOwner: clearOwner,
  } = await import('../../../dist/Execution/Fission/Runtime.js')
  const { observeLaneTurn } = await import('../../../dist/Execution/Fission/OpenCode/Host.js')
  const { observe, observeIdle } = await import('../../../dist/Composition/Turn/OrdinaryTurnWorkflow.js')
  const { ReconciledTurn, ReconciledTurnContext, ReconciledTurnDelivery } = await import('../../../dist/Composition/Turn/Observation.js')
  const { PhysicalUserMessageIdModule_create: physicalId, AuthorityRootUserMessageIdModule_create: rootId, ProviderRunIdentityModule_create: runId } = await import('../../../dist/Foundation/Identity.js')
  const { TurnOutcome, SnapshotObservation } = await import('../../../dist/Composition/Turn/Program.js')

  const owner = sessionId('retired-owner-workflow')
  markSilentInterrupt(owner)

  try {
    const turn = new ReconciledTurn(
      owner,
      physicalId('msg-1'),
      rootId('msg-0'),
      runId('run-1'),
      undefined,
      undefined,
      [],
      undefined,
      undefined,
      undefined,
      TurnOutcome.TurnInProgress,
      undefined,
    )

    let continuationSent = false
    let terminalNotified = false

    const sessionPort = {
      SendPrompt: async () => {
        continuationSent = true
        return { tag: 0, fields: [] }
      },
      AbortChildren: async () => {},
    }
    const eventPort = {
      NotifyTerminal: () => {
        terminalNotified = true
      },
    }

    // FissionHost.observeLaneTurn must absorb owner turn (return true)
    const handled = await observeLaneTurn(sessionPort, eventPort, undefined, new Set(), turn)
    assert.equal(handled, true, 'Fission owner turn must be handled/absorbed by FissionHost')

    // OrdinaryTurnWorkflow.observe must short-circuit without continuing
    const context = new ReconciledTurnContext(turn, undefined, ReconciledTurnDelivery.Observation)
    await observe(
      undefined,
      () => {},
      sessionPort,
      eventPort,
      undefined,
      new Set(),
      () => false,
      new Set(),
      undefined,
      undefined,
      context,
    )
    assert.equal(continuationSent, false, 'observe must not send continuations for retired Fission owner')
    assert.equal(terminalNotified, false, 'observe must not publish terminal for retired Fission owner')

    // OrdinaryTurnWorkflow.observeIdle must also short-circuit
    const idleContext = new ReconciledTurnContext(turn, undefined, ReconciledTurnDelivery.IdleRevisit)
    await observeIdle(undefined, sessionPort, eventPort, undefined, idleContext)
    assert.equal(continuationSent, false, 'observeIdle must not send continuations for retired Fission owner')
  } finally {
    clearOwner(owner)
  }
})
