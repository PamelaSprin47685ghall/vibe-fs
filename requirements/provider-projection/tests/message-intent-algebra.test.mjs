import assert from 'node:assert/strict'
import test from 'node:test'

import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'

const textMessage = (role, text) => ({ role, parts: [{ kind: 'text', text }] })
const emptySnapshot = () => Projection.projectionSnapshot(Projection.semanticProjection([]))
const row = (role, text, hostMessageId) => ({
  message: textMessage(role, text),
  hostMessageId,
  hostIsPhysical: false,
})

test('WHAT[PROVIDER-PROJECTION-005] generic message intents replace feature-owned constructors', () => {
  assert.equal(typeof Projection.replaceMessageBase, 'function')
  assert.equal(typeof Projection.insertMessageRows, 'function')
  assert.equal(Projection.useStrengthMirror, undefined)
  assert.equal(Projection.strengthCandidate, undefined)
  assert.equal(Projection.strengthPromoted, undefined)
  assert.equal(Projection.strengthReplicaLocal, undefined)
})

test('WHAT[PROVIDER-PROJECTION-006] different generic message bases conflict', () => {
  const first = Projection.replaceMessageBase({
    key: 'owner-base-a',
    rows: [row('user', 'first', 'first-id')],
  })
  const second = Projection.replaceMessageBase({
    key: 'owner-base-b',
    rows: [row('user', 'second', 'second-id')],
  })

  assert.deepEqual(Projection.plan([first, second]), {
    ok: false,
    conflict: 'ConflictingMessageBase',
  })
})

test('WHAT[PROVIDER-PROJECTION-006] generic row insertion is registration-order independent', () => {
  const base = [
    textMessage('user', 'u1'),
    textMessage('assistant', 'a1'),
    textMessage('user', 'u2'),
  ]
  const first = Projection.insertMessageRows({
    key: 'a-first',
    anchor: { kind: 'BeforeMessageIndex', index: 1 },
    rows: [row('assistant', 'inserted-a', 'insert-a')],
  })
  const second = Projection.insertMessageRows({
    key: 'b-second',
    anchor: { kind: 'BeforeMessageIndex', index: 2 },
    rows: [row('assistant', 'inserted-b', 'insert-b')],
  })

  const forward = Projection.renderMessagesWithHostIds(emptySnapshot(), base, [first, second])
  const reverse = Projection.renderMessagesWithHostIds(emptySnapshot(), base, [second, first])

  assert.deepEqual(reverse, forward)
  assert.deepEqual(forward.hostMessageIds, [null, 'insert-a', null, 'insert-b', null])
  assert.equal(Projection.renderWire(forward.messages), Projection.renderWire(reverse.messages))
})
