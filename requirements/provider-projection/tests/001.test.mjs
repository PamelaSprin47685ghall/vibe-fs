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
