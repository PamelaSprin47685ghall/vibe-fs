// INTERACTION-AUTHORITY proof — repair projection uses one production instruction constant.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Participant/Provider/Projection/Surface.js'

const raw = [{ role: 'user', parts: [{ kind: 'text', text: 'base' }] }]
const snapshot = () => projection.projectionSnapshot(projection.semanticProjection(raw), {})

test('WHAT[INTERACTION-AUTHORITY-010] PROJ_008_InsertRepair_uses_the_production_instruction', () => {
  assert.equal(typeof projection.repairInstruction, 'string')
  const rendered = projection.renderMessages(snapshot(), raw, [projection.insertRepair('rk-prod-1')])
  assert.equal(rendered.length, 2)
  assert.equal(rendered[0].parts[0].text, 'base')
  assert.equal(rendered[1].role, 'user')
  assert.equal(rendered[1].parts[0].text, projection.repairInstruction)
})
