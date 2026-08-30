// P4 pilot: QuiescenceSurface — opaque capability + behavior surface proof.
// owner: crash-reconciliation. HOST-004 idle-derived continuation admission.
//
// JS-SEMANTIC-SURFACE-002/003/005: the registered surface is the legal entry
// point; `gate` and `permit` are opaque handles (obtain → pass back → dispose),
// never inspected. Session ids cross as plain strings.

import assert from 'node:assert/strict'
import test from 'node:test'
import { assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'

const quiescence = await import('../../../dist/OpenCode/Host/QuiescenceSurface.js')

const S = 'ses-q'
const accepted = { accepted: true, failure: null }
const rejected = (failure) => ({ accepted: false, failure })

test('WHAT[CRASH-006] Q01_normal_stable_idle_yields_one_consumable_permit', () => {
  const gate = quiescence.create()
  assertOpaque(gate, 'gate')
  quiescence.beginAttempt(gate, S)
  const permit = quiescence.observeIdle(gate, S)
  assertOpaque(permit, 'permit')

  assert.deepEqual(quiescence.tryConsume(gate, permit), accepted, 'fresh idle permit must consume once')
  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('AlreadyConsumed'), 'a consumed permit must never send again')
})

test('WHAT[CRASH-006] Q02_new_provider_attempt_invalidates_the_old_permit', () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  const permit = quiescence.observeIdle(gate, S)

  // The core race: attempt B's transform begins BEFORE the old reconcile's
  // side effect executes.
  quiescence.beginAttempt(gate, S)

  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('Superseded'), 'stale permit must be rejected')
})

test('WHAT[CRASH-006] Q03_repeated_idle_does_not_repeat_send', () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  const first = quiescence.observeIdle(gate, S)
  const second = quiescence.observeIdle(gate, S)

  assert.deepEqual(quiescence.tryConsume(gate, first), accepted)
  assert.deepEqual(quiescence.tryConsume(gate, second), rejected('AlreadyConsumed'), 'the same idle occasion admits at most one send')
})

test('WHAT[CRASH-006] Q04_new_attempt_own_idle_can_send_again', () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  const aPermit = quiescence.observeIdle(gate, S)
  assert.deepEqual(quiescence.tryConsume(gate, aPermit), accepted)

  // A fresh attempt gets its own fresh idle right — a consumed permit never
  // permanently suppresses the session.
  quiescence.beginAttempt(gate, S)
  const bPermit = quiescence.observeIdle(gate, S)
  assert.deepEqual(quiescence.tryConsume(gate, bPermit), accepted, 'B must be able to send on its own idle')
})

test('WHAT[CRASH-006] Q05_new_physical_user_material_revokes_the_previous_idle_before_transform', () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  const oldPermit = quiescence.observeIdle(gate, S)

  quiescence.observePhysicalMessage(gate, S, 'msg-new')

  assert.deepEqual(
    quiescence.tryConsume(gate, oldPermit),
    rejected('Revoked'),
    'physical user admission must close the old idle-send window before messages.transform starts',
  )

  quiescence.beginAttempt(gate, S)
  const newPermit = quiescence.observeIdle(gate, S)

  // chat.message can be replayed for the same physical material. The ingress
  // barrier is exact-message idempotent and must not revoke the live attempt.
  quiescence.observePhysicalMessage(gate, S, 'msg-new')
  assert.deepEqual(quiescence.tryConsume(gate, newPermit), accepted, 'same physical message replay must be a no-op')
})

test('WHAT[CRASH-006] Q05b_delayed_older_physical_replay_is_inert_after_newer_material', () => {
  const gate = quiescence.create()

  quiescence.observePhysicalMessage(gate, S, 'msg-a')
  quiescence.beginAttempt(gate, S)
  quiescence.observePhysicalMessage(gate, S, 'msg-b')
  quiescence.beginAttempt(gate, S)
  const currentPermit = quiescence.observeIdle(gate, S)

  // Delivery of A may lag behind the already-observed A → B sequence. Every
  // physical id seen in this session is replay evidence, not only the latest.
  quiescence.observePhysicalMessage(gate, S, 'msg-a')
  assert.deepEqual(quiescence.tryConsume(gate, currentPermit), accepted, 'delayed replay of A must not revoke B\'s live attempt')
})

test('WHAT[CRASH-006] Q06_definitive_pre_acceptance_rejection_can_return_the_same_idle_permit', () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  const permit = quiescence.observeIdle(gate, S)

  assert.deepEqual(quiescence.tryConsume(gate, permit), accepted)
  assert.deepEqual(quiescence.tryRelease(gate, permit), accepted, 'definite no-send may re-open the exact idle serial')
  assert.deepEqual(quiescence.tryConsume(gate, permit), accepted, 'the gate reminder may retry after a definite Host rejection')

  quiescence.beginAttempt(gate, S)
  assert.deepEqual(
    quiescence.tryRelease(gate, permit),
    rejected('Superseded'),
    'a fresher provider attempt prevents an old consumed permit from being resurrected',
  )
})

test('WHAT[CRASH-001] Q07_restart_gate_holds_no_permit', () => {
  const before = quiescence.create()
  quiescence.beginAttempt(before, S)
  const oldPermit = quiescence.observeIdle(before, S)

  // New process incarnation: the gate is empty, so the old permit is unknown
  // to it and no idle-derived continuation can pass.
  const after = quiescence.create()
  assert.deepEqual(quiescence.tryConsume(after, oldPermit), rejected('WrongOwner'), 'restart must not inherit idle truth')
})

test('WHAT[CRASH-001] Q08_restart_or_unknown_idle_cannot_mint_new_send_authority', () => {
  const restarted = quiescence.create()
  const historicalIdle = quiescence.observeIdle(restarted, S)
  assert.deepEqual(
    quiescence.tryConsume(restarted, historicalIdle),
    rejected('NoFreshIdle'),
    'SessionIdle without a current-process BeginProviderAttempt is historical observation, not continuation authority',
  )

  quiescence.beginAttempt(restarted, S)
  const freshIdle = quiescence.observeIdle(restarted, S)
  assert.deepEqual(quiescence.tryConsume(restarted, freshIdle), accepted, 'a real current-process provider attempt restores idle authority')
})

test('WHAT[CRASH-006] Q10_session_deleted_drops_every_permit', () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  const permit = quiescence.observeIdle(gate, S)

  quiescence.dropSession(gate, S)
  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('NoFreshIdle'), 'a dropped session never sends on an old permit')
})

test('WHAT[CRASH-008] ESC_P0_2_operator_abort_revokes_unconsumed_idle_permit', () => {
  // HOST-004: a permit is minted on fresh idle but not yet consumed; Esc
  // revokes the attempt. A delayed reconcile must NOT be able to consume the
  // old permit (which is what would mint a bare `#` missing-final-report repair).
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)
  const permit = quiescence.observeIdle(gate, S)

  quiescence.revoke(gate, S)

  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('Revoked'), 'abort must permanently void the pending idle permit')
})

test('WHAT[CRASH-008] ESC_P0_3_aborted_attempt_cannot_be_reminted_by_delayed_idle', () => {
  // After Esc, a delayed SessionIdle must NOT re-establish a usable idle
  // permit for the aborted attempt; eligibility returns only with the next
  // real BeginProviderAttempt (HOST-004).
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, S)

  quiescence.revoke(gate, S)
  const latePermit = quiescence.observeIdle(gate, S)

  assert.deepEqual(quiescence.tryConsume(gate, latePermit), rejected('Revoked'), 'revoked attempt must not mint a usable idle permit')

  // A genuine new attempt restores eligibility.
  quiescence.beginAttempt(gate, S)
  const freshPermit = quiescence.observeIdle(gate, S)
  assert.deepEqual(quiescence.tryConsume(gate, freshPermit), accepted, 'next real BeginProviderAttempt re-establishes idle rights')
})

test('WHAT[CRASH-006] P4_SURFACE_exports_exact_capability_names', () => {
  assert.deepEqual(Object.getOwnPropertyNames(quiescence).sort(), [
    'beginAttempt',
    'create',
    'dropSession',
    'livePermitCount',
    'observeIdle',
    'observePhysicalMessage',
    'revoke',
    'tryConsume',
    'tryRelease',
  ])
})
