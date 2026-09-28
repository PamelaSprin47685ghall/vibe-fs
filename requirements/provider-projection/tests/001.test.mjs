import assert from 'node:assert/strict'
import test from 'node:test'
import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'

test('WHAT[provider-projection-001] repeated rendering of equal generic inputs gives equal output', () => {
  const current = [{ role: 'user', parts: [{ kind: 'text', text: 'base' }] }]
  const snapshot = Projection.projectionSnapshot(Projection.semanticProjection(current))
  const intent = Projection.insertMessageRows({ key: 'sample', anchor: { kind: 'Append' }, rows: [{
    message: { role: 'assistant', parts: [{ kind: 'text', text: 'projected' }] },
    hostMessageId: 'projected-id', hostIsPhysical: false,
  }] })
  const first = Projection.renderMessagesWithHostIds(snapshot, current, [intent])
  const repeated = Projection.renderMessagesWithHostIds(structuredClone(snapshot), structuredClone(current), [structuredClone(intent)])
  assert.deepEqual(repeated, first)
  assert.equal(Projection.renderWire(repeated.messages), Projection.renderWire(first.messages))
})

test.todo('WHAT[provider-projection-001] real online and durable replay use the same pipeline; cloning the same input is not a replay execution (GAP-082)')
