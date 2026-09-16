import assert from 'node:assert/strict'
import test from 'node:test'

import * as WorkflowSurface from '../../../dist/Composition/Turn/WorkflowSurface.js'

test('WHAT[STRUCTURED-WORKFLOW-001] SW_001_workflow_entrypoints_are_the_exported_surface', () => {
  const exported = Object.keys(WorkflowSurface).sort()
  assert.ok(exported.includes('observe'), 'WorkflowSurface must export observe')
  assert.ok(exported.includes('observeIdle'), 'WorkflowSurface must export observeIdle')
  assert.ok(exported.includes('handleCompleted'), 'WorkflowSurface must export handleCompleted')
  assert.ok(exported.includes('handleOutcome'), 'WorkflowSurface must export handleOutcome')
  assert.ok(exported.includes('handleFailedTurn'), 'WorkflowSurface must export handleFailedTurn')
  assert.ok(exported.includes('isTerminalOutcome'), 'WorkflowSurface must export isTerminalOutcome')
  assert.ok(exported.includes('decideStep'), 'WorkflowSurface must export decideStep')
  assert.ok(exported.includes('publishDecision'), 'WorkflowSurface must export publishDecision')
  assert.ok(exported.includes('scheduleNextTurn'), 'WorkflowSurface must export scheduleNextTurn')
  assert.ok(exported.includes('advanceWithJournal'), 'WorkflowSurface must export advanceWithJournal')
})
