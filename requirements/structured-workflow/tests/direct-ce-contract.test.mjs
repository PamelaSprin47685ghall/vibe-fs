import assert from 'node:assert/strict'
import test from 'node:test'
import { scanText } from '../../../scripts/checks/dsl-ownership.mjs'

test('WHAT[STRUCTURED-WORKFLOW-001] FLOW_001_direct_task_workflow_is_allowed', () => {
  const source = [
    'module Workflow',
    'let run readSnapshot = task {',
    '    let! snapshot = readSnapshot ()',
    '    return snapshot',
    '}',
  ].join('\n')

  assert.deepEqual(scanText(source, 'Application/Workflow.fs'), [])
})

test('WHAT[STRUCTURED-WORKFLOW-002] FLOW_006_second_runtime_patterns_are_rejected', () => {
  const samples = [
    ['type WorkflowCommand =', 'second-runtime-protocol'],
    ['type WorkflowReply =', 'second-runtime-protocol'],
    ['    | Step of WorkflowCommand * (WorkflowReply -> Workflow)', 'second-runtime-protocol'],
    ["    | Suspend of 'instruction * (obj -> Workflow)", 'second-runtime-protocol'],
    ['module WorkflowInterpreter =', 'business-interpreter'],
  ]

  for (const [source, expectedGate] of samples) {
    assert.deepEqual(
      scanText(source, 'Application/Workflow.fs').map(({ gate }) => gate),
      [expectedGate],
      source,
    )
  }
})
