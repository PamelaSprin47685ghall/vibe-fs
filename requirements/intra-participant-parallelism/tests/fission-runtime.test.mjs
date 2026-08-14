import assert from 'node:assert/strict'
import test from 'node:test'
import { caseOf, listItems, payloadOf } from '../../verification-system/tests/support/domain.mjs'
import { SessionIdModule_create as sessionId, SessionIdModule_value as sessionValue } from '../../../dist/Kernel/Identity.js'

const Fission = await import('../../../dist/Domain/Fission.js')
const Admission = await import('../../../dist/Session/FissionAdmission.js')

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
  return { events, runtime: Admission.create(deps) }
}

test('admission creates fresh sibling sessions with old parent and starts from LWR + exact lane input before interrupt', async () => {
  const { events, runtime } = harness()
  const owner = sessionId('old-caller')
  const result = await Admission.admit(runtime, owner, parsed())
  assert.equal(caseOf(result), 'Ok')

  const creates = events.filter(([kind]) => kind === 'create')
  assert.deepEqual(creates.map(([, index, parent]) => [index, parent]), [[0, 'old-parent'], [1, 'old-parent']])
  const starts = events.filter(([kind]) => kind === 'start')
  assert.equal(starts.length, 2)
  assert.match(starts[0][3], /CANONICAL-LWR/)
  assert.match(starts[0][3], /lane A  /, 'lane input spaces are preserved')
  assert.match(starts[1][3], /lane B/)

  const interruptAt = events.findIndex(([kind]) => kind === 'silent-interrupt')
  assert.ok(interruptAt > events.findLastIndex(([kind]) => kind === 'start'), 'old caller interrupts only after every lane started')
  assert.equal(Admission.isActive(runtime, owner), true)
})

test('root caller produces sibling roots rather than making lanes children of old caller', async () => {
  const { events, runtime } = harness({ parent: null })
  const owner = sessionId('root-caller')
  assert.equal(caseOf(await Admission.admit(runtime, owner, parsed())), 'Ok')
  assert.deepEqual(events.filter(([k]) => k === 'create').map((e) => e[2]), [null, null])
})

test('partial create or start failure rolls back every created lane and never interrupts old caller', async () => {
  for (const options of [{ failCreateAt: 1 }, { failStartAt: 1 }]) {
    const { events, runtime } = harness(options)
    const owner = sessionId(`owner-${JSON.stringify(options)}`)
    const result = await Admission.admit(runtime, owner, parsed())
    assert.equal(caseOf(result), 'Error')
    assert.equal(events.some(([kind]) => kind === 'silent-interrupt'), false)
    const created = events.filter(([kind]) => kind === 'create' && !events.some(() => false)).length
    const rolledBack = events.filter(([kind]) => kind === 'rollback').length
    assert.ok(rolledBack >= 1)
    assert.equal(Admission.isActive(runtime, owner), false)
  }
})

test('failed silent interrupt rolls back lanes; second admission while active is rejected until release', async () => {
  const failed = harness({ failInterrupt: true })
  const failedOwner = sessionId('interrupt-owner')
  assert.equal(caseOf(await Admission.admit(failed.runtime, failedOwner, parsed())), 'Error')
  assert.equal(failed.events.filter(([k]) => k === 'rollback').length, 2)
  assert.equal(Admission.isActive(failed.runtime, failedOwner), false)

  const live = harness()
  const owner = sessionId('single-flight-owner')
  assert.equal(caseOf(await Admission.admit(live.runtime, owner, parsed())), 'Ok')
  const second = await Admission.admit(live.runtime, owner, parsed())
  assert.equal(caseOf(second), 'Error')
  assert.equal(caseOf(payloadOf(second)), 'AlreadyFissioned')
  Admission.release(live.runtime, owner)
  assert.equal(Admission.isActive(live.runtime, owner), false)
})
