import assert from 'node:assert/strict'
import test from 'node:test'
import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'

const message = (role, text) => ({ role, parts: [{ kind: 'text', text }] })
const textMessage = message
const emptySnapshot = () => Projection.projectionSnapshot(Projection.semanticProjection([]))
const row = (role, text, hostMessageId = null, hostIsPhysical = false) => ({
  message: message(role, text),
  hostMessageId,
  hostIsPhysical,
})

const H = (text) => `H(${text})`
const snapshot = (messages = []) => Projection.projectionSnapshot(Projection.semanticProjection(messages))
const base = (key, rows) => Projection.replaceMessageBase({ key, rows })
const insert = (key, anchor, rows) => Projection.insertMessageRows({ key, anchor, rows })
const before = (index) => ({ kind: 'BeforeMessageIndex', index })
const append = { kind: 'Append' }

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

test('WHAT[PROVIDER-PROJECTION-006] identical message bases dedupe deterministically', () => {
  const intent = base('base-1', [row('user', 'replacement', 'host-base', true)])

  assert.deepEqual(Projection.plan([intent, intent]), {
    ok: true,
    intents: ['ReplaceMessageBase'],
  })
})

test('WHAT[PROVIDER-PROJECTION-006] different message bases conflict regardless of registration order', () => {
  const left = base('base-left', [row('user', 'left')])
  const right = base('base-right', [row('user', 'right')])

  assert.deepEqual(Projection.plan([left, right]), {
    ok: false,
    conflict: 'ConflictingMessageBase',
  })
  assert.deepEqual(Projection.plan([right, left]), {
    ok: false,
    conflict: 'ConflictingMessageBase',
  })
})

test('WHAT[PROVIDER-PROJECTION-006] identical same-key row insertions dedupe', () => {
  const intent = insert('rows-1', before(0), [row('assistant', 'inserted', 'host-insert')])

  assert.deepEqual(Projection.plan([intent, intent]), {
    ok: true,
    intents: ['InsertMessageRows'],
  })
})

test('WHAT[PROVIDER-PROJECTION-006] differing same-key row insertions conflict with the key', () => {
  const left = insert('same-key', before(0), [row('assistant', 'left')])
  const right = insert('same-key', append, [row('assistant', 'right')])

  assert.deepEqual(Projection.plan([left, right]), {
    ok: false,
    conflict: 'ConflictingMessageRows',
    key: 'same-key',
  })
})

test('WHAT[PROVIDER-PROJECTION-006] base and row intents have canonical permutation-invariant order', () => {
  const intents = [
    insert('z-append', append, [row('assistant', 'append-z', 'z')]),
    insert('b-before', before(1), [row('assistant', 'before-b', 'b')]),
    base('base', [row('user', 'zero', 'zero', true), row('user', 'one', 'one', true)]),
    insert('a-before', before(1), [row('assistant', 'before-a', 'a')]),
    insert('a-append', append, [row('assistant', 'append-a', 'aa')]),
  ]

  const forward = Projection.renderMessagesWithHostIds(snapshot(), [], intents)
  const reverse = Projection.renderMessagesWithHostIds(snapshot(), [], [...intents].reverse())

  assert.deepEqual(reverse, forward)
  assert.deepEqual(forward.messages.map(item => item.parts[0].text), [
    'zero',
    'before-a',
    'before-b',
    'one',
    'append-a',
    'append-z',
  ])
})
