import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
import test from 'node:test'
import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'

const sha256 = (text) => createHash('sha256').update(text, 'utf8').digest('hex')
const projection = (callId, args = '{"path":"x"}') => ({
  providerId: null, modelId: null, variant: null, tools: [], system: [],
  messages: [{ role: 'assistant', parts: [{ kind: 'tool-call', callId, name: 'read', args }] }],
})

test('WHAT[provider-projection-011] semantic digest is invariant to physical call IDs but sensitive to actual arguments', () => {
  const first = projection('call-first')
  const second = projection('call-second')
  const changed = projection('call-third', '{"path":"y"}')
  assert.equal(Projection.semanticallyEqual(first, second), true)
  assert.equal(Projection.semanticallyEqual(first, changed), false)
  assert.notEqual(Projection.renderWire(first.messages), Projection.renderWire(second.messages))
  const digest = (value) => Projection.cutoffDigest(sha256, Projection.projectionSnapshot(Projection.semanticProjection(value.messages)), 1)
  assert.equal(digest(first), sha256(Projection.renderSemantic(first)))
  assert.match(digest(first), /^[0-9a-f]{64}$/)
  assert.equal(digest(first), digest(second))
  assert.notEqual(digest(first), digest(changed))
})

test('WHAT[provider-projection-011] cutoff digest includes exactly the selected semantic prefix', () => {
  const messages = ['first', 'second', 'third'].map((text) => ({ role: 'user', parts: [{ kind: 'text', text }] }))
  const snapshot = Projection.projectionSnapshot(Projection.semanticProjection(messages))
  const expected = Projection.renderSemantic(Projection.semanticProjection(messages.slice(0, 2)))
  assert.equal(Projection.cutoffDigest(sha256, snapshot, 2), sha256(expected))
  assert.notEqual(Projection.cutoffDigest(sha256, snapshot, 2), Projection.cutoffDigest(sha256, snapshot, 3))
})

test.todo('WHAT[provider-projection-011] production composition supplies SHA-256 and excludes every transport-only field; injected hash tests cover the projection boundary only (GAP-082)')
