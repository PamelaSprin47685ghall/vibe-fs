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
