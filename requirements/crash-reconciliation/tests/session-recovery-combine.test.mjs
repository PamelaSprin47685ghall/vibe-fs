// rabbit §14.2 — SessionRecovery.combine algebra.
// Dist-dependent: run after BUILD CLEAR / serial rebuild.

import assert from 'node:assert/strict'
import test from 'node:test'

import { caseOf } from '../../../tests/unit/support/domain.mjs'

const mod = await import('../../../dist/Domain/SessionRecovery.js')
const {
  SessionRecovery,
  RecoveryBlock,
  NonEmpty_one: nonEmptyOne,
  RecoveryReceiptModule_create: makeReceipt,
} = mod
const combine = mod.combine ?? mod.SessionRecovery_combine ?? mod.SessionRecoveryModule_combine

const { SessionIdModule_create: sid } = await import('../../../dist/Kernel/Identity.js')

const receipt = (id = 's') => makeReceipt(sid(id), 1n, null, [], [])
const blocked = (reason = 'x') =>
  new SessionRecovery(3, [nonEmptyOne(new RecoveryBlock(0, [sid('b'), reason]))])
const waiting = (reason = 'w') =>
  new SessionRecovery(2, [nonEmptyOne(new RecoveryBlock(5, [sid('c'), reason]))])
const recovered = () => new SessionRecovery(1, [receipt('r')])
const ready = () => new SessionRecovery(0, [receipt('n')])

test('RECOVERY_COMBINE_export_exists', () => {
  assert.equal(typeof combine, 'function', 'SessionRecovery.combine missing from dist — rebuild needed')
})

test('RECOVERY_COMBINE_blocked_dominates', () => {
  const out = combine([ready(), waiting(), blocked('hard'), recovered()])
  assert.equal(caseOf(out), 'Blocked')
})

test('RECOVERY_COMBINE_waiting_dominates_ready', () => {
  const out = combine([ready(), recovered(), waiting('pending')])
  assert.equal(caseOf(out), 'Waiting')
})

test('RECOVERY_COMBINE_recovered_over_ready', () => {
  const out = combine([ready(), recovered()])
  assert.equal(caseOf(out), 'Recovered')
})

test('RECOVERY_COMBINE_empty_is_no_recovery_required', () => {
  const out = combine([])
  assert.equal(caseOf(out), 'NoRecoveryRequired')
})

test('RECOVERY_COMBINE_order_independent_for_tier', () => {
  const a = combine([blocked('a'), waiting(), recovered()])
  const b = combine([recovered(), blocked('a'), waiting()])
  assert.equal(caseOf(a), 'Blocked')
  assert.equal(caseOf(b), 'Blocked')
})
