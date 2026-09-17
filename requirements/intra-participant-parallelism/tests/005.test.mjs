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

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-005] old caller silent-interrupts only after every lane started', async () => {
  const { events, runtime } = harness()
  const owner = 'old-caller-interrupt-order'
  const result = await fission.admit(runtime, owner, parsed())
  assert.equal(result.ok, true, JSON.stringify(result))

  const interruptAt = events.findIndex(([kind]) => kind === 'silent-interrupt')
  assert.ok(
    interruptAt > events.findLastIndex(([kind]) => kind === 'start'),
    'old caller interrupts only after every lane started',
  )
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-005] failed silent interrupt rolls back lanes and old caller stays out of active set', async () => {
  const failed = harness({ failInterrupt: true })
  const failedOwner = 'interrupt-owner'
  assert.equal((await fission.admit(failed.runtime, failedOwner, parsed())).ok, false)
  assert.equal(failed.events.filter(([k]) => k === 'rollback').length, 2)
  assert.equal(fission.isActive(failed.runtime, failedOwner), false)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-005] FissionRuntime preserves silent interrupt across multiple checks and is cleared only by clearOwner/clearSilentInterrupt', async () => {
  const owner = 'retired-owner-1'
  assert.equal(fission.isSilentInterrupt(owner), false)

  fission.markSilentInterrupt(owner)
  assert.equal(fission.isSilentInterrupt(owner), true)
  assert.equal(fission.tryConsumeSilentInterrupt(owner), true)
  // Must NOT be cleared after consuming once:
  assert.equal(fission.isSilentInterrupt(owner), true)
  assert.equal(fission.tryConsumeSilentInterrupt(owner), true)

  fission.clearSilentInterrupt(owner)
  assert.equal(fission.isSilentInterrupt(owner), false)

  fission.markSilentInterrupt(owner)
  assert.equal(fission.isSilentInterrupt(owner), true)
  fission.clearOwner(owner)
  assert.equal(fission.isSilentInterrupt(owner), false)
})
