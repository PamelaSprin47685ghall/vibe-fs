import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

test('WHAT[CHGINT-011] HOST_JoinPublishedAvailable_engine_init_failure_is_an_error_result', async () => {
  const host = change.createOrchestratorHost({
    sweepDirty: () => Promise.resolve({ ok: false, error: 'sweep fail' }),
  })
  const res = await change.hostJoinPublishedAvailable(host, 10)
  assert.equal(res.ok, false)
  assert.match(res.error, /sweep fail/i)
})
