import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData, assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'

const fission = await import('../../../dist/Execution/Fission/Surface.js')

const fissionHost = await import('../../../dist/OpenCode/Host/FissionHostSurface.js')

const parsed = () => fission.parsePrompt([' lane A  ', 'lane B'])

const harness = ({ failCreateAt, failStartAt, failInterrupt = false, parent = 'old-parent' } = {}) => {
  const events = []
  let serial = 0
  const runtime = fission.createAdmission({
    parentOf: async (owner) => {
      events.push(['parent', owner])
      return parent
    },
    ownerWorkRecord: async (owner) => {
      events.push(['lwr', owner])
      return 'CANONICAL-LWR'
    },
    createLane: async (_owner, physicalParent, lane) => {
      events.push(['create', lane.index, physicalParent])
      if (lane.index === failCreateAt) throw new Error(`create-${lane.index}`)
      serial += 1
      return `lane-${serial}`
    },
    startLane: async (laneSession, startup) => {
      const index = Number(/lane_index = (\d+)/.exec(startup)?.[1])
      events.push(['start', index, laneSession, startup])
      if (index === failStartAt) throw new Error(`start-${index}`)
    },
    abortLane: async (laneSession) => {
      events.push(['rollback', laneSession])
    },
    silentInterruptOwner: async (owner) => {
      events.push(['silent-interrupt', owner])
      if (failInterrupt) throw new Error('interrupt-failed')
    },
  })
  return { events, runtime }
}

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-004] partial create or start failure rolls back every created lane and never interrupts old caller', async () => {
  for (const options of [{ failCreateAt: 1 }, { failStartAt: 1 }]) {
    const { events, runtime } = harness(options)
    const owner = `owner-${JSON.stringify(options)}`
    const result = await fission.admit(runtime, owner, parsed())
    assert.equal(result.ok, false)
    assert.equal(
      events.some(([kind]) => kind === 'silent-interrupt'),
      false,
    )
    const rolledBack = events.filter(([kind]) => kind === 'rollback').length
    assert.ok(rolledBack >= 1)
    assert.equal(fission.isActive(runtime, owner), false)
  }
})
