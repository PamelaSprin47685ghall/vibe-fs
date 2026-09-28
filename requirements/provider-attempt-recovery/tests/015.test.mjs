import assert from 'node:assert/strict'
import test from 'node:test'
import * as planner from '../../../dist/Participant/Provider/Attempt/PlannerSurface.js'
import { current, run } from './support/retry.mjs'

test('WHAT[provider-attempt-recovery-015] retry engine never admits or redispatches a StrengthReplica failure', async () => {
  for (const failure of ['ProviderTransient', 'ProviderPermanent']) {
    const attempt = run(failure, current(), 'RetryAuthorized', 'StrengthReplica')
    await attempt.completed
    assert.deepEqual(attempt.events, [])
  }
})

test('WHAT[provider-attempt-recovery-015] StrengthReplica success is ineligible to clear the owner failure count', () => {
  const plan = planner.plan({ role: 'engineer', kind: 'strength-replica' })
  assert.equal(plan.ok, true)
  assert.equal(plan.requestKind, 'strength-replica')
  assert.equal(plan.clearsFailureCountOnSuccess, false)
})

test.todo('WHAT[provider-attempt-recovery-015] actual speculative success and failure leave the owner journal budget unchanged (GAP-139)')
