import assert from 'node:assert/strict'
import test from 'node:test'
import * as change from '../../../dist/Change/Surface.js'
import { stepIntegrationScenario } from './support/gate-scope-fixture.mjs'

test('WHAT[CHGINT-014] stale certificate never reaches publish gate', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'await:Candidate', snapshotId: 's-stale', certificateId: 'cert-1', rebaseNeeded: false, currentWorkspace: 's-fresh' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('gate:')).length, 0)
})

test('WHAT[CHGINT-014] Git conflict facts override model-perfect publication', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'await:Candidate', snapshotId: 's-1', certificateId: 'cert-perfect-score', unmerged: true },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('gate:')).length, 0)
})

test('WHAT[CHGINT-014] stale certificate fails closed and never enters publish gate', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'await:Candidate', snapshotId: 's-old', certificateId: 'cert-1', rebaseNeeded: false, currentWorkspace: 's-new' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('gate:')).length, 0)
})

test('WHAT[CHGINT-014] reentry with wrong original-vs-rebased snapshot sends zero FF', async () => {
  const result = await stepIntegrationScenario([
    { kind: 'reentry:wrong-snapshot' },
  ])
  assert.equal(result.timeline.filter((s) => s.startsWith('ff:')).length, 0)
})
