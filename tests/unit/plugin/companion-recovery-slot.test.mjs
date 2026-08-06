/**
 * CTX-006 / FALLBACK-012: Companion recovery slot is a one-shot physical signal.
 *
 * The slot is the Y half of HostSignalBootstrap.ArmRecovery: a real failure
 * arms it, the squash decision reads `IsRecoveryArmed`, starting a squash
 * clears it. It is deliberately NOT a Task waiter — nothing in production
 * awaits it, and cancelling an un-awaited promise would reject it into an
 * unhandled rejection. A restart leaves it NotArmed. These tests walk the
 * public dist surface (Companion class) — no runtime tag, no internals.
 */
import test from 'node:test'
import assert from 'node:assert/strict'
import { sessionId } from '../support/domain.mjs'

const Companion = await import('../../../dist/Session/Companion.js')

const make = () => Companion.Companion_$ctor_Z79B603FF(undefined, undefined, sessionId('ses-main'))

test('CTX_006_recovery_slot_starts_not_armed', () => {
  const c = make()
  assert.equal(Companion.Companion__get_IsRecoveryArmed(c), false)
})

test('CTX_006_arm_is_a_one_shot_signal_disarm_clears_it', () => {
  const c = make()
  Companion.Companion__ArmRecoverySlot(c)
  assert.equal(Companion.Companion__get_IsRecoveryArmed(c), true)

  Companion.Companion__DisarmRecoverySlot(c)
  assert.equal(Companion.Companion__get_IsRecoveryArmed(c), false, 'disarm clears atomically')
})

test('CTX_006_second_failure_does_not_create_second_opportunity', () => {
  // A second failure while the first slot is unconsumed is not a second
  // recovery opportunity: arming is idempotent, one disarm fully clears it.
  const c = make()
  Companion.Companion__ArmRecoverySlot(c)
  Companion.Companion__ArmRecoverySlot(c)
  assert.equal(Companion.Companion__get_IsRecoveryArmed(c), true)
  Companion.Companion__DisarmRecoverySlot(c)
  assert.equal(Companion.Companion__get_IsRecoveryArmed(c), false)
})

test('CTX_006_disarm_when_not_armed_is_a_noop', () => {
  const c = make()
  Companion.Companion__DisarmRecoverySlot(c)
  assert.equal(Companion.Companion__get_IsRecoveryArmed(c), false)
})
