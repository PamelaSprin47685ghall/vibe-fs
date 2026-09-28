import assert from 'node:assert/strict'
import test from 'node:test'
import * as sessions from '../../../dist/OpenCode/Host/SessionsSurface.js'

for (const rejectAbort of [false, true]) {
  test(`WHAT[managed-session-lifecycle-017] actual termination waits for descendant drain and Host outcome before exact Failed, Host rejection=${rejectAbort}`, async () => {
    const observed = await sessions.terminationProbe(rejectAbort)
    assert.equal(observed.rootRejected, true)
    assert.equal(observed.rootEffects, 0)
    assert.deepEqual(Array.from(observed.beforeDrain), [0, 0])
    assert.equal(observed.beforeAbort, 0)
    assert.deepEqual(observed.cancelled, ['termination-child'])
    assert.deepEqual(observed.aborted, ['termination-child'])
    assert.deepEqual(observed.terminals, [{
      session: 'termination-child', kind: 'Failed', reason: 'no successor', authority: 'physical-authority',
    }])
    assert.equal(observed.ok, !rejectAbort)
    assert.equal(observed.error, rejectAbort ? 'Host abort failed: controlled Host rejected AbortSession' : '')
  })
}

test.todo('WHAT[managed-session-lifecycle-017] real successor workflows and parent listeners reject missing or wrong Authority Root without orphaning parent wait (GAP-133)')
