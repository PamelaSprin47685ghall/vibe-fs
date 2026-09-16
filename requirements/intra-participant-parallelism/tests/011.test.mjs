import assert from 'node:assert/strict'
import test from 'node:test'

const fission = await import('../../../dist/Execution/Fission/Surface.js')
const parsed = () => fission.parsePrompt([' lane A  ', 'lane B'])

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-011] second admission while active is rejected as AlreadyFissioned until release', async () => {
  let serial = 0
  const runtime = fission.createAdmission({
    parentOf: async () => 'old-parent',
    ownerWorkRecord: async () => 'CANONICAL-LWR',
    createLane: async () => {
      serial += 1
      return `lane-${serial}`
    },
    startLane: async () => {},
    abortLane: async () => {},
    silentInterruptOwner: async () => {},
  })

  const owner = 'single-flight-owner'
  assert.equal((await fission.admit(runtime, owner, parsed())).ok, true)
  const second = await fission.admit(runtime, owner, parsed())
  assert.equal(second.ok, false)
  assert.equal(second.reason, 'AlreadyFissioned')
  fission.release(runtime, owner)
  assert.equal(fission.isActive(runtime, owner), false)
})
