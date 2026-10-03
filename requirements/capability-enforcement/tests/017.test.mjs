import assert from 'node:assert/strict'
import test from 'node:test'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'

test('WHAT[capability-enforcement-017] released one-shot opportunity cannot be consumed again', {
  todo: '10-D1: capability-enforcement-017 closes a consumed/released one-shot identity, but crash-reconciliation-006 and dispatch-protocol-007 require the exact permit to be returned after proven pre-acceptance rejection; the gate has no rejection witness or separate retry-grant identity',
}, () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, 'multiplicity-conflict')
  const permit = quiescence.observeIdle(gate, 'multiplicity-conflict')
  assert.equal(quiescence.tryConsume(gate, permit).accepted, true)
  assert.equal(quiescence.tryRelease(gate, permit).accepted, true)
  assert.equal(quiescence.tryConsume(gate, permit).accepted, false)
})

const accepted = { accepted: true, failure: null }
const rejected = (failure) => ({ accepted: false, failure })

const freshPermit = (gate, session) => {
  quiescence.beginAttempt(gate, session)
  return quiescence.observeIdle(gate, session)
}

test('WHAT[capability-enforcement-017] duplicate idle callbacks share one opportunity while a new attempt on the same session remains legal', () => {
  const gate = quiescence.create()
  const session = 'same-session-new-attempt'
  const first = freshPermit(gate, session)
  const duplicateIdle = quiescence.observeIdle(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, first), accepted)
  assert.deepEqual(quiescence.tryConsume(gate, duplicateIdle), rejected('AlreadyConsumed'))
  assert.deepEqual(quiescence.tryConsume(gate, quiescence.observeIdle(gate, session)), rejected('AlreadyConsumed'))

  const current = freshPermit(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, first), rejected('Superseded'))
  assert.deepEqual(quiescence.tryRelease(gate, first), rejected('Superseded'))
  assert.deepEqual(quiescence.tryConsume(gate, current), accepted, 'a consumed prior identity must not permanently ban this session')
  assert.deepEqual(quiescence.tryConsume(gate, current), rejected('AlreadyConsumed'))
})

test('WHAT[capability-enforcement-017] release of a superseded consumed identity cannot reopen or release the new occupied opportunity', () => {
  const gate = quiescence.create()
  const session = 'old-callback-new-owner'
  const old = freshPermit(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, old), accepted)
  const current = freshPermit(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, current), accepted)

  for (let replay = 0; replay < 2; replay += 1) {
    assert.deepEqual(quiescence.tryRelease(gate, old), rejected('Superseded'))
    assert.deepEqual(quiescence.tryConsume(gate, old), rejected('Superseded'))
    assert.deepEqual(quiescence.tryConsume(gate, current), rejected('AlreadyConsumed'), 'a stale release must not reopen the current occupied identity')
  }

  const next = freshPermit(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, next), accepted)
})

test('WHAT[capability-enforcement-017] physical ingress, tool completion and deletion preserve fresh admission without reviving retired identities', () => {
  const gate = quiescence.create()
  const session = 'normal-quiescence-lifecycle'
  quiescence.observePhysicalMessage(gate, session, 'physical-first')
  const first = freshPermit(gate, session)
  quiescence.beginTool(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, first), rejected('NoFreshIdle'))
  quiescence.endTool(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, first), accepted)

  quiescence.observePhysicalMessage(gate, session, 'physical-next')
  assert.deepEqual(quiescence.tryRelease(gate, first), rejected('Revoked'))
  const current = freshPermit(gate, session)
  quiescence.observePhysicalMessage(gate, session, 'physical-first')
  assert.deepEqual(quiescence.tryConsume(gate, current), accepted, 'late old ingress is not a new message and must not revoke fresh work')

  quiescence.dropSession(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, current), rejected('NoFreshIdle'))
  const reused = freshPermit(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, current), rejected('Superseded'))
  assert.deepEqual(quiescence.tryRelease(gate, current), rejected('Superseded'))
  assert.deepEqual(quiescence.tryConsume(gate, reused), accepted, 'fresh same-session identity must be admitted without deleting the old serial tombstone')
})
