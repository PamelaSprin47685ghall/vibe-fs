import assert from 'node:assert/strict'
import test from 'node:test'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'

const session = 'session'
const accepted = { accepted: true, failure: null }
const rejected = failure => ({ accepted: false, failure })
const fresh = () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, session)
  return { gate, permit: quiescence.observeIdle(gate, session) }
}

test('WHAT[crash-reconciliation-006] one stable idle permits one consumption despite repeated idle observations', () => {
  const { gate, permit } = fresh()
  const repeated = quiescence.observeIdle(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, permit), accepted)
  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('AlreadyConsumed'))
  assert.deepEqual(quiescence.tryConsume(gate, repeated), rejected('AlreadyConsumed'))
})

test('WHAT[crash-reconciliation-006] new attempt invalidates the old permit and creates its own idle right', () => {
  const { gate, permit } = fresh()
  quiescence.beginAttempt(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('Superseded'))
  const current = quiescence.observeIdle(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, current), accepted)
  quiescence.beginAttempt(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, quiescence.observeIdle(gate, session)), accepted)
})

test('WHAT[crash-reconciliation-006] transport idle becomes consumable only after the last active tool ends', () => {
  const gate = quiescence.create()
  quiescence.beginAttempt(gate, session)
  quiescence.beginTool(gate, session)
  quiescence.beginTool(gate, session)
  const permit = quiescence.observeIdle(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('NoFreshIdle'))
  quiescence.endTool(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('NoFreshIdle'))
  quiescence.endTool(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, permit), accepted)
})

test('WHAT[crash-reconciliation-006] new physical material revokes old idle but exact and older replay remain inert', () => {
  const { gate, permit } = fresh()
  quiescence.observePhysicalMessage(gate, session, 'msg-a')
  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('Revoked'))
  quiescence.beginAttempt(gate, session)
  const first = quiescence.observeIdle(gate, session)
  quiescence.observePhysicalMessage(gate, session, 'msg-a')
  assert.deepEqual(quiescence.tryConsume(gate, first), accepted)
  quiescence.observePhysicalMessage(gate, session, 'msg-b')
  quiescence.beginAttempt(gate, session)
  const current = quiescence.observeIdle(gate, session)
  quiescence.observePhysicalMessage(gate, session, 'msg-a')
  assert.deepEqual(quiescence.tryConsume(gate, current), accepted)
})

test('WHAT[crash-reconciliation-006] exact consumed idle can be returned until a newer attempt or physical message supersedes it', () => {
  for (const change of ['attempt', 'message']) {
    const { gate, permit } = fresh()
    assert.deepEqual(quiescence.tryConsume(gate, permit), accepted)
    assert.deepEqual(quiescence.tryRelease(gate, permit), accepted)
    assert.deepEqual(quiescence.tryConsume(gate, permit), accepted)
    if (change === 'attempt') quiescence.beginAttempt(gate, session)
    else quiescence.observePhysicalMessage(gate, session, 'new-message')
    assert.equal(quiescence.tryRelease(gate, permit).accepted, false)
  }
})

test('WHAT[crash-reconciliation-006] session deletion revokes its permit without affecting another session', () => {
  const { gate, permit } = fresh()
  quiescence.beginAttempt(gate, 'other')
  const other = quiescence.observeIdle(gate, 'other')
  quiescence.dropSession(gate, session)
  assert.deepEqual(quiescence.tryConsume(gate, permit), rejected('NoFreshIdle'))
  assert.deepEqual(quiescence.tryConsume(gate, other), accepted)
})

test.todo('WHAT[crash-reconciliation-006] real sending boundary returns a permit only for proven pre-acceptance rejection and never for acceptance unknown (GAP-149)')
