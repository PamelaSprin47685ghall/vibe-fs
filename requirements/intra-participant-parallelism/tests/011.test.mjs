import assert from 'node:assert/strict'
import test from 'node:test'
import { fission, harness, parsed, deferred } from './support/admission.mjs'

test('WHAT[intra-participant-parallelism-011] reservation rejects a second admission even before the first finishes and is reusable after release', async () => {
  const reached = deferred()
  const release = deferred()
  const { events, runtime } = harness({ beforeStart: async index => {
    if (index === 0) {
      reached.resolve()
      await release.promise
    }
  } })
  const owner = 'single-flight-owner'
  const first = fission.admit(runtime, owner, parsed())
  try {
    await reached.promise
    const created = events.filter(([kind]) => kind === 'created').length
    assert.deepEqual(await fission.admit(runtime, owner, parsed()), { ok: false, reason: 'AlreadyFissioned' })
    assert.equal(events.filter(([kind]) => kind === 'created').length, created)
    release.resolve()
    assert.equal((await first).ok, true)
    assert.deepEqual(await fission.admit(runtime, owner, parsed()), { ok: false, reason: 'AlreadyFissioned' })
    fission.release(runtime, owner)
    assert.equal(fission.isActive(runtime, owner), false)
    assert.equal((await fission.admit(runtime, owner, parsed())).ok, true)
  } finally {
    release.resolve()
    await first
    fission.release(runtime, owner)
  }
})

test.todo('WHAT[intra-participant-parallelism-011] a real active lane maps back to its logical owner and cannot recursively create another group (GAP-158)')
