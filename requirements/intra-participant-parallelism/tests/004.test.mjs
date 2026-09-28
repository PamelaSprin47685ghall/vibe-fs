import assert from 'node:assert/strict'
import test from 'node:test'
import { fission, harness } from './support/admission.mjs'

test('WHAT[intra-participant-parallelism-004] each create/start failure rolls back exactly all created lanes without interrupting the original caller', async () => {
  for (const failure of ['failCreateAt', 'failStartAt']) {
    for (const index of [0, 1, 2]) {
      const { events, runtime } = harness({ [failure]: index })
      const owner = `rollback-${failure}-${index}`
      try {
        const result = await fission.admit(runtime, owner, fission.parsePrompt(['A', 'B', 'C']))
        assert.equal(result.ok, false)
        assert.equal(events.some(([kind]) => kind === 'silent-interrupt'), false)
        assert.deepEqual(events.filter(([kind]) => kind === 'rollback').map(([, session]) => session), events.filter(([kind]) => kind === 'created').map(([, session]) => session))
        assert.equal(fission.isActive(runtime, owner), false)
      } finally {
        fission.release(runtime, owner)
      }
    }
  }
})

test.todo('WHAT[intra-participant-parallelism-004] actual Host binding and dispatch failures roll back durable admission and every created lane, including cleanup failure and uncertain acceptance (GAP-158)')
