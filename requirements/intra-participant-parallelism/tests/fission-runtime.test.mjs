import assert from 'node:assert/strict'
import test from 'node:test'

const fission = await import('../../../dist/Execution/Fission/Surface.js')
const fissionHost = await import('../../../dist/OpenCode/Host/FissionHostSurface.js')

const parsed = () => fission.parsePrompt(' lane A  \nlane B')

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

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-003] admission creates fresh sibling sessions with old parent and starts from LWR + exact lane input', async () => {
  const { events, runtime } = harness()
  const owner = 'old-caller'
  const result = await fission.admit(runtime, owner, parsed())
  assert.equal(result.ok, true, JSON.stringify(result))

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

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-013] user-facing root caller is rejected before fission reserves or creates anything', async () => {
  const { events, runtime } = harness({ parent: null })
  const owner = 'root-caller'
  const result = await fission.admit(runtime, owner, parsed())

  assert.equal(result.ok, false)
  assert.equal(result.reason, 'InvalidOrigin')
  assert.deepEqual(events, [['parent', 'root-caller']])
  assert.equal(fission.isActive(runtime, owner), false)
})

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

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-005] failed silent interrupt rolls back lanes and old caller stays out of active set', async () => {
  const failed = harness({ failInterrupt: true })
  const failedOwner = 'interrupt-owner'
  assert.equal((await fission.admit(failed.runtime, failedOwner, parsed())).ok, false)
  assert.equal(failed.events.filter(([k]) => k === 'rollback').length, 2)
  assert.equal(fission.isActive(failed.runtime, failedOwner), false)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-011] second admission while active is rejected as AlreadyFissioned until release', async () => {
  const live = harness()
  const owner = 'single-flight-owner'
  assert.equal((await fission.admit(live.runtime, owner, parsed())).ok, true)
  const second = await fission.admit(live.runtime, owner, parsed())
  assert.equal(second.ok, false)
  assert.equal(second.reason, 'AlreadyFissioned')
  fission.release(live.runtime, owner)
  assert.equal(fission.isActive(live.runtime, owner), false)
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

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-009] observeLaneTurn and OrdinaryTurnWorkflow absorb Fission-replaced owner turns without sending continuations', async () => {
  const owner = 'retired-owner-workflow'
  fission.markSilentInterrupt(owner)

  try {
    const observed = await fissionHost.observeReplacedOwner(owner)
    assert.equal(observed.handled, true, 'Fission owner turn must be handled/absorbed by FissionHost')
    assert.equal(observed.continuationSent, false, 'observe must not send continuations for retired Fission owner')
    assert.equal(observed.terminalNotified, false, 'observe must not publish terminal for retired Fission owner')
  } finally {
    fission.clearOwner(owner)
  }
})
