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
