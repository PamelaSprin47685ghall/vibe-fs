import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'

test('WHAT[CHGINT-011] HOST_JoinPublishedAvailable_engine_init_failure_is_an_error_result', async () => {
  const result = await change.gitFreezeTargetBranch(change.createGit('/repo', () => Promise.resolve([128, '', 'bad repo'])))
  assert.equal(result.ok, false)
  assert.match(result.error, /bad repo/)
})
