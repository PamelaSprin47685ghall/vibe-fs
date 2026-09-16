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

test('WHAT[PROVIDER-PROJECTION-002] snapshot contains only the current semantic projection', () => {
  const currentProjection = Projection.semanticProjection([message('user', 'current')])
  const value = Projection.projectionSnapshot(currentProjection)

  assert.deepEqual(Object.keys(value), ['currentProjection'])
  assert.deepEqual(value.currentProjection, currentProjection)
})
