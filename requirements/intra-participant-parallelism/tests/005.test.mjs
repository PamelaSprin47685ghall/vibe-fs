import assert from 'node:assert/strict'
import test from 'node:test'
import { fission, harness, parsed, deferred } from './support/admission.mjs'

test('WHAT[intra-participant-parallelism-005] original caller cannot be interrupted while the final lane is still starting', async () => {
  const reached = deferred()
  const release = deferred()
  const { events, runtime } = harness({ beforeStart: async index => {
    if (index === 1) {
      reached.resolve()
      await release.promise
    }
  } })
  const owner = 'old-caller-interrupt-order'
  const admission = fission.admit(runtime, owner, parsed())
  try {
    await reached.promise
    assert.equal(events.filter(([kind]) => kind === 'started').length, 1)
    assert.equal(events.some(([kind]) => kind === 'silent-interrupt'), false)
    release.resolve()
    assert.equal((await admission).ok, true)
    assert.equal(events.filter(([kind]) => kind === 'silent-interrupt').length, 1)
    assert.ok(events.findIndex(([kind]) => kind === 'silent-interrupt') > events.findLastIndex(([kind]) => kind === 'started'))
  } finally {
    release.resolve()
    await admission
    fission.release(runtime, owner)
  }
})

test('WHAT[intra-participant-parallelism-005] interrupt rejection rolls back all admitted lanes and releases the reservation', async () => {
  const { events, runtime } = harness({ failInterrupt: true })
  const owner = 'interrupt-owner'
  assert.equal((await fission.admit(runtime, owner, parsed())).ok, false)
  assert.deepEqual(events.filter(([kind]) => kind === 'rollback').map(([, id]) => id), events.filter(([kind]) => kind === 'created').map(([, id]) => id))
  assert.equal(fission.isActive(runtime, owner), false)
})

test('WHAT[intra-participant-parallelism-005] runtime silent-interrupt marker survives reads until explicit owner cleanup', () => {
  const owner = 'silent-marker-owner'
  try {
    assert.equal(fission.isSilentInterrupt(owner), false)
    fission.markSilentInterrupt(owner)
    assert.equal(fission.isSilentInterrupt(owner), true)
    assert.equal(fission.tryConsumeSilentInterrupt(owner), true)
    assert.equal(fission.isSilentInterrupt(owner), true)
    assert.equal(fission.tryConsumeSilentInterrupt(owner), true)
    fission.clearSilentInterrupt(owner)
    assert.equal(fission.isSilentInterrupt(owner), false)
    fission.markSilentInterrupt(owner)
    fission.clearOwner(owner)
    assert.equal(fission.isSilentInterrupt(owner), false)
  } finally {
    fission.clearOwner(owner)
  }
})

test.todo('WHAT[intra-participant-parallelism-005] actual old Host execution retires silently without business Aborted, child cancellation or recovery dispatch (GAP-158)')
