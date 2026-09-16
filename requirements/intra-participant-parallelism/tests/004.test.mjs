import assert from 'node:assert/strict'
import test from 'node:test'

const fission = await import('../../../dist/Execution/Fission/Surface.js')
const parsed = () => fission.parsePrompt([' lane A  ', 'lane B'])

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-004] partial create or start failure rolls back every created lane and never interrupts old caller', async () => {
  for (const options of [{ failCreateAt: 1 }, { failStartAt: 1 }]) {
    const events = []
    let serial = 0
    const runtime = fission.createAdmission({
      parentOf: async (owner) => {
        events.push(['parent', owner])
        return 'old-parent'
      },
      ownerWorkRecord: async (owner) => {
        events.push(['lwr', owner])
        return 'CANONICAL-LWR'
      },
      createLane: async (_owner, physicalParent, lane) => {
        events.push(['create', lane.index, physicalParent])
        if (lane.index === options.failCreateAt) throw new Error(`create-${lane.index}`)
        serial += 1
        return `lane-${serial}`
      },
      startLane: async (laneSession, startup) => {
        const index = Number(/lane_index = (\d+)/.exec(startup)?.[1])
        events.push(['start', index, laneSession, startup])
        if (index === options.failStartAt) throw new Error(`start-${index}`)
      },
      abortLane: async (laneSession) => {
        events.push(['rollback', laneSession])
      },
      silentInterruptOwner: async (owner) => {
        events.push(['silent-interrupt', owner])
      },
    })

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
