import assert from 'node:assert/strict'
import test from 'node:test'
import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'

const freezeTree = (value) => {
  if (value && typeof value === 'object') {
    for (const nested of Object.values(value)) freezeTree(nested)
    Object.freeze(value)
  }
  return value
}

test('WHAT[provider-projection-002] snapshot contains only a copied current semantic projection', () => {
  const input = Projection.semanticProjection([{ role: 'user', parts: [{ kind: 'text', text: 'original' }] }])
  const snapshot = Projection.projectionSnapshot(input)
  assert.deepEqual(Object.keys(snapshot), ['currentProjection'])
  assert.deepEqual(snapshot.currentProjection, input)
  input.messages[0].parts[0].text = 'changed externally'
  assert.equal(snapshot.currentProjection.messages[0].parts[0].text, 'original')
})

test('WHAT[provider-projection-002] planning and rendering accept frozen snapshots, messages and intents without mutating caller data', () => {
  const messages = freezeTree([{ role: 'user', parts: [{ kind: 'text', text: 'original' }] }])
  const snapshot = freezeTree(Projection.projectionSnapshot(Projection.semanticProjection(messages)))
  const intents = freezeTree([Projection.insertMessageRows({ key: 'append', anchor: { kind: 'Append' }, rows: [{
    message: { role: 'assistant', parts: [{ kind: 'text', text: 'added' }] }, hostMessageId: null, hostIsPhysical: false,
  }] })])
  const before = JSON.stringify({ messages, snapshot, intents })
  assert.equal(Projection.plan(intents).ok, true)
  const rendered = Projection.renderMessagesWithHostIds(snapshot, messages, intents)
  assert.deepEqual(rendered.messages.map((message) => message.parts[0].text), ['original', 'added'])
  assert.equal(JSON.stringify({ messages, snapshot, intents }), before)
})
