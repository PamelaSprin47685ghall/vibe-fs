import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData, assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'

const fission = await import('../../../dist/Execution/Fission/Surface.js')
const parsed = () => fission.parsePrompt([' lane A  ', 'lane B'])

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-003] admission creates fresh sibling sessions with old parent and starts from LWR + exact lane input', async () => {
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
      serial += 1
      return `lane-${serial}`
    },
    startLane: async (laneSession, startup) => {
      const index = Number(/lane_index = (\d+)/.exec(startup)?.[1])
      events.push(['start', index, laneSession, startup])
    },
    abortLane: async (laneSession) => {
      events.push(['rollback', laneSession])
    },
    silentInterruptOwner: async (owner) => {
      events.push(['silent-interrupt', owner])
    },
  })

  assertOpaque(runtime, 'Fission admission runtime')
  const owner = 'old-caller'
  const result = await fission.admit(runtime, owner, parsed())
  assertJsData(result, 'Fission admission result')
  assert.equal(result.ok, true, JSON.stringify(result))

  assert.deepEqual(result.lanes, [
    { index: 0, prompt: ' lane A  ' },
    { index: 1, prompt: 'lane B' },
  ])
  assert.equal('ownerSessionId' in result, false)
  assert.equal('parentSessionId' in result, false)
  assert.equal('sessionId' in result.lanes[0], false)

  const creates = events.filter(([kind]) => kind === 'create')
  assert.deepEqual(
    creates.map(([, index, parent]) => [index, parent]),
    [
      [0, 'old-parent'],
      [1, 'old-parent'],
    ],
  )
  const starts = events.filter(([kind]) => kind === 'start')
  assert.equal(starts.length, 2)
  assert.match(starts[0][3], /CANONICAL-LWR/)
  assert.match(starts[0][3], /lane A  /, 'lane input spaces are preserved')
  assert.match(starts[1][3], /lane B/)
  assert.equal(fission.isActive(runtime, owner), true)
})
