// ENF-019: Process capability/permit lifecycle and fresh attempt admission
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
