import assert from 'node:assert/strict'
import test from 'node:test'
import { assertJsData, assertOpaque } from '../../verification-system/tests/support/js-contract.mjs'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'

const SESSION = 'ses-process-capability'
const accepted = { accepted: true, failure: null }
const rejected = (failure) => ({ accepted: false, failure })

const assertResult = (actual, expected) => {
  assertJsData(actual, 'quiescence result')
  assert.deepEqual(actual, expected)
}

const freshPermit = (gate, session = SESSION) => {
  assertOpaque(gate, 'quiescence gate')
  quiescence.beginAttempt(gate, session)
  const permit = quiescence.observeIdle(gate, session)
  assertOpaque(permit, 'quiescence permit')
  return permit
}

test('WHAT[ENF-018] process capability consumes once and reports duplicate consumption without effect', () => {
  const gate = quiescence.create()
  const permit = freshPermit(gate)

  assertResult(quiescence.tryConsume(gate, permit), accepted)
  assertResult(quiescence.tryConsume(gate, permit), rejected('AlreadyConsumed'))
  assertResult(quiescence.tryConsume(gate, permit), rejected('AlreadyConsumed'))
})

test('WHAT[ENF-018] process capability is owner-bound and a wrong-owner error leaves the issuing gate unchanged', () => {
  const owner = quiescence.create()
  const stranger = quiescence.create()
  const permit = freshPermit(owner)

  assertResult(quiescence.tryConsume(stranger, permit), rejected('WrongOwner'))
  assertResult(quiescence.tryRelease(stranger, permit), rejected('WrongOwner'))
  assertResult(quiescence.tryConsume(owner, permit), accepted)
})

test('WHAT[ENF-018] a newer attempt supersedes only the stale permit', () => {
  const gate = quiescence.create()
  const stale = freshPermit(gate)
  const current = freshPermit(gate)

  assertResult(quiescence.tryConsume(gate, stale), rejected('Superseded'))
  assertResult(quiescence.tryRelease(gate, stale), rejected('Superseded'))
  assertResult(quiescence.tryConsume(gate, current), accepted)
})

test('WHAT[ENF-018] revoke is permanent for that attempt and a real newer attempt restores eligibility', () => {
  const gate = quiescence.create()
  const revoked = freshPermit(gate)

  quiescence.revoke(gate, SESSION)
  assertResult(quiescence.tryConsume(gate, revoked), rejected('Revoked'))
  const delayedIdle = quiescence.observeIdle(gate, SESSION)
  assertOpaque(delayedIdle, 'delayed idle permit')
  assertResult(quiescence.tryConsume(gate, delayedIdle), rejected('Revoked'))

  const next = freshPermit(gate)
  assertResult(quiescence.tryConsume(gate, next), accepted)
})

test('WHAT[ENF-018] absent or dropped fresh-idle state fails closed without changing later admission', () => {
  const gate = quiescence.create()
  const historicalIdle = quiescence.observeIdle(gate, SESSION)
  assertOpaque(historicalIdle, 'historical idle permit')

  assertResult(quiescence.tryConsume(gate, historicalIdle), rejected('NoFreshIdle'))

  const dropped = freshPermit(gate)
  quiescence.dropSession(gate, SESSION)
  assertResult(quiescence.tryConsume(gate, dropped), rejected('NoFreshIdle'))

  const current = freshPermit(gate)
  assertResult(quiescence.tryConsume(gate, current), accepted)
})

test('WHAT[ENF-018] release reopens only an exact consumed permit and every rejected release is inert', () => {
  const gate = quiescence.create()
  const permit = freshPermit(gate)

  assertResult(quiescence.tryRelease(gate, permit), rejected('NoFreshIdle'))
  assertResult(quiescence.tryConsume(gate, permit), accepted)
  assertResult(quiescence.tryRelease(gate, permit), accepted)
  assertResult(quiescence.tryConsume(gate, permit), accepted)
})

test('WHAT[ENF-019] provider-attempt composition requires fresh current-process admission without codec or event recovery', () => {
  const priorProcess = quiescence.create()
  const priorPermit = freshPermit(priorProcess)
  const currentProcess = quiescence.create()

  assertResult(quiescence.tryConsume(currentProcess, priorPermit), rejected('WrongOwner'))

  const unownedIdle = quiescence.observeIdle(currentProcess, SESSION)
  assertOpaque(unownedIdle, 'unowned idle permit')
  assertResult(quiescence.tryConsume(currentProcess, unownedIdle), rejected('NoFreshIdle'))

  const currentPermit = freshPermit(currentProcess)
  assertResult(quiescence.tryConsume(currentProcess, currentPermit), accepted)
})

test('WHAT[ENF-019] live opaque permit resources stay bounded to the current session attempt', () => {
  const gate = quiescence.create()
  assert.equal(quiescence.livePermitCount(gate), 0)

  for (let attempt = 0; attempt < 256; attempt += 1) {
    quiescence.beginAttempt(gate, SESSION)
    const first = quiescence.observeIdle(gate, SESSION)
    const replay = quiescence.observeIdle(gate, SESSION)
    assertOpaque(first, 'current idle permit')
    assertOpaque(replay, 'replayed current idle permit')
    assert.equal(quiescence.livePermitCount(gate), 1, 'idle replay and attempt churn must not grow the resource registry')
  }

  quiescence.revoke(gate, SESSION)
  assert.equal(quiescence.livePermitCount(gate), 0, 'revocation must release current-attempt permit resources')

  freshPermit(gate)
  quiescence.dropSession(gate, SESSION)
  assert.equal(quiescence.livePermitCount(gate), 0, 'session cleanup must release permit resources')
})
