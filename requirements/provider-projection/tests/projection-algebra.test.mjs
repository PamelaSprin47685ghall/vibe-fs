import assert from 'node:assert/strict'
import test from 'node:test'

import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'

const H = (text) => `H(${text})`
const message = (role, text) => ({ role, parts: [{ kind: 'text', text }] })
const row = (role, text, hostMessageId = null, hostIsPhysical = false) => ({
  message: message(role, text),
  hostMessageId,
  hostIsPhysical,
})
const snapshot = (messages = []) => Projection.projectionSnapshot(Projection.semanticProjection(messages))
const base = (key, rows) => Projection.replaceMessageBase({ key, rows })
const insert = (key, anchor, rows) => Projection.insertMessageRows({ key, anchor, rows })
const before = (index) => ({ kind: 'BeforeMessageIndex', index })
const append = { kind: 'Append' }

test('WHAT[PROVIDER-PROJECTION-001] online and replay projection share one canonical generic renderer', () => {
  const current = [message('user', 'base')]
  const intent = insert('replayable', append, [row('assistant', 'projected', 'projected-id')])
  const online = Projection.renderMessagesWithHostIds(snapshot(), current, [intent])
  const replay = Projection.renderMessagesWithHostIds(
    snapshot(),
    structuredClone(current),
    [structuredClone(intent)],
  )

  assert.deepEqual(replay, online)
  assert.equal(Projection.renderWire(replay.messages), Projection.renderWire(online.messages))
})

test('WHAT[PROVIDER-PROJECTION-002] snapshot contains only the current semantic projection', () => {
  const currentProjection = Projection.semanticProjection([message('user', 'current')])
  const value = Projection.projectionSnapshot(currentProjection)

  assert.deepEqual(Object.keys(value), ['currentProjection'])
  assert.deepEqual(value.currentProjection, currentProjection)
})

test('WHAT[PROVIDER-PROJECTION-005] surface exposes only generic projection constructors', () => {
  assert.equal(typeof Projection.replaceMessageBase, 'function')
  assert.equal(typeof Projection.insertMessageRows, 'function')
  assert.equal(typeof Projection.plan, 'function')
  assert.equal(typeof Projection.renderMessagesWithHostIds, 'function')
  assert.equal(Projection.keepPhysicalPrefix, undefined)
  assert.equal(Projection.activatePrefixEpoch, undefined)
  assert.equal(Projection.insertBlogFrames, undefined)
  assert.equal(Projection.insertRepair, undefined)
  assert.equal(Projection.useStrengthMirror, undefined)
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

test('WHAT[PROVIDER-PROJECTION-004] base replacement preserves every Host metadata channel', () => {
  const rendered = Projection.renderMessagesWithHostIds(
    snapshot(),
    [message('user', 'ignored')],
    [base('base', [
      row('user', 'physical', 'physical-id', true),
      row('assistant', 'synthetic', 'synthetic-id', false),
      row('user', 'anonymous'),
    ])],
  )

  assert.deepEqual(rendered.messages.map(item => item.parts[0].text), ['physical', 'synthetic', 'anonymous'])
  assert.deepEqual(rendered.hostMessageIds, ['physical-id', 'synthetic-id', null])
  assert.deepEqual(rendered.hostIsPhysical, [true, false, false])
})

test('WHAT[PROVIDER-PROJECTION-004] BeforeMessageIndex and Append materialize aligned rows', () => {
  const original = [message('user', 'first'), message('user', 'second')]
  const intents = [
    insert('before', before(1), [row('assistant', 'middle', 'middle-id')]),
    insert('append', append, [row('assistant', 'last', 'last-id')]),
  ]

  const rendered = Projection.renderMessagesWithHostIds(snapshot(original), original, intents)

  assert.deepEqual(rendered.messages.map(item => item.parts[0].text), ['first', 'middle', 'second', 'last'])
  assert.deepEqual(rendered.hostMessageIds, [null, 'middle-id', null, 'last-id'])
  assert.deepEqual(rendered.hostIsPhysical, [false, false, false, false])
  assert.deepEqual(Projection.renderMessages(snapshot(original), original, intents), rendered.messages)
})

test('WHAT[PROVIDER-PROJECTION-004] canonical wire rendering freezes the generic row shape', () => {
  const rendered = Projection.renderMessages(
    snapshot(),
    [],
    [base('base', [row('user', 'hello'), row('assistant', 'world')])],
  )

  assert.equal(
    Projection.renderWire(rendered),
    '{"provider":null,"model":null,"variant":null,"tools":[],"system":[],"messages":[{"role":"user","parts":[{"kind":"text","text":"hello"}]},{"role":"assistant","parts":[{"kind":"text","text":"world"}]}]}',
  )
})

test('WHAT[PROVIDER-PROJECTION-004] cutoff digest hashes only the truncated current projection', () => {
  const current = snapshot([
    message('user', 'first'),
    message('assistant', 'second'),
    message('user', 'third'),
  ])
  const expectedProjection = Projection.semanticProjection([
    message('user', 'first'),
    message('assistant', 'second'),
  ])

  assert.equal(Projection.cutoffDigest(H, current, 2), H(Projection.renderSemantic(expectedProjection)))
})

test('WHAT[PROVIDER-PROJECTION-011] PROJ_003_semantic_equality_ignores_wire_ids_but_wire_bytes_differ', () => {
  const projection = (callId) => ({
    providerId: null,
    modelId: null,
    variant: null,
    tools: [],
    system: [],
    messages: [{
      role: 'assistant',
      parts: [{ kind: 'tool-call', callId, name: 'read', argumentsCanonical: '{"path":"x"}' }],
    }],
  })
  const first = projection('call-first')
  const second = projection('call-second')

  assert.equal(Projection.semanticallyEqual(first, second), true)
  assert.notEqual(Projection.renderWire(first.messages), Projection.renderWire(second.messages))
})

test('WHAT[PROVIDER-PROJECTION-003] rendered rows equal the decode of their Host writeback shape', () => {
  const intent = base('base', [
    row('user', 'physical', 'physical-id', true),
    row('assistant', 'synthetic', 'synthetic-id', false),
  ])
  const rendered = Projection.renderMessagesWithHostIds(snapshot(), [], [intent])
  const hostWriteback = rendered.messages.map((item, index) => ({
    info: {
      id: rendered.hostMessageIds[index],
      role: item.role,
    },
    parts: item.parts.map(part => ({ type: part.kind, text: part.text })),
  }))

  assert.deepEqual(Projection.decodeMessages(hostWriteback).messages, rendered.messages)
  assert.equal(
    Projection.renderWire(Projection.decodeMessages(hostWriteback).messages),
    Projection.renderWire(rendered.messages),
  )
})
