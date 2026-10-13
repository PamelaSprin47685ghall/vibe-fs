import assert from 'node:assert/strict'
import test from 'node:test'
import { END_TOOL, EVENTS, RESULT_NAMES, REVIEW_OUTCOMES } from './support/contract.mjs'

test('WHAT[work-001] live names are Work events, Relayed, Delivered, Promoted, Stayed, deliver', () => {
  assert.deepEqual(RESULT_NAMES, ['Relayed', 'Delivered'])
  assert.deepEqual(REVIEW_OUTCOMES, ['Promoted', 'Stayed'])
  assert.equal(END_TOOL, 'deliver')
  assert.equal(EVENTS.includes('WorkOpened'), true)
  assert.equal(EVENTS.includes('QualityCertificateInvalidated'), false)
  assert.equal(EVENTS.includes('PlanDelivered'), false)
  assert.equal(END_TOOL === 'suicide', false)
})
