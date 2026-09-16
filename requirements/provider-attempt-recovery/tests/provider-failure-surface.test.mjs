// Provider failure surface: budget + projection deduplication and success reset.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  budget as providerFailureBudget,
  providerFailureProjection,
} from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'

const SESSION = 'ses_meta'

test('WHAT[PAR-003] ProviderFailure_owner_exposes_budget_and_dedupe_state', () => {
  const initial = providerFailureProjection.forAuthority('run_L', 'msg_u1')
  const ownerIdentity = providerFailureBudget.attemptIdentity(SESSION, 'run_L', 'msg_u1', 'run_owner')
  const secondIdentity = providerFailureBudget.attemptIdentity(SESSION, 'run_L', 'msg_u1', 'run_second')
  const ownerAdvance = providerFailureProjection.applyFailure(ownerIdentity, 1, initial)
  assert.equal(ownerAdvance.ok, true, ownerAdvance.ok ? '' : ownerAdvance.error)
  assert.deepEqual(providerFailureProjection.read(ownerAdvance.value), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    failures: 1,
    dedupeKeys: 1,
    exhausted: false,
  })

  const secondAdvance = providerFailureProjection.applyFailure(secondIdentity, 2, ownerAdvance.value)
  assert.equal(secondAdvance.ok, true, secondAdvance.ok ? '' : secondAdvance.error)
  assert.deepEqual(providerFailureProjection.read(secondAdvance.value), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    failures: 2,
    dedupeKeys: 2,
    exhausted: false,
  })

  const duplicate = providerFailureProjection.applyFailure(ownerIdentity, 2, secondAdvance.value)
  assert.equal(duplicate.ok, false)
  assert.equal(duplicate.error, 'AlreadyObserved')

  assert.deepEqual(providerFailureProjection.read(providerFailureProjection.recordSuccess(ownerAdvance.value)), {
    logicalRun: 'run_L',
    authorityRoot: 'msg_u1',
    failures: 0,
    dedupeKeys: 0,
    exhausted: false,
  })
})
