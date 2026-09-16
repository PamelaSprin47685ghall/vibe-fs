import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

import * as WorkflowSurface from '../../../dist/Composition/Turn/WorkflowSurface.js'
import * as OutcomeSurface from '../../../dist/Foundation/OutcomeSurface.js'
import * as ReconcileSurface from '../../../dist/Composition/Turn/ReconcileSurface.js'
import * as RecoveryReentrySurface from '../../../dist/Composition/Turn/RecoveryReentrySurface.js'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')

test('WHAT[STRUCTURED-WORKFLOW-003] SW_002_workflow_modules_export_no_program_counter_shaped_names', () => {
  const exported = Object.keys(WorkflowSurface)
  const forbiddenPcNames = [
    'NextAction',
    'NextStep',
    'ResumeAt',
    'StepIndex',
    'ContinueToken',
    'Stage',
    'Phase',
    'ExecutionSlot',
    'CurrentStep',
    'ProgramCounter',
  ]
  for (const name of forbiddenPcNames) {
    assert.equal(
      exported.includes(name),
      false,
      `WorkflowSurface must not export program counter name: ${name}`,
    )
  }
})

test('WHAT[STRUCTURED-WORKFLOW-003] SW_003_domain_flow_and_outcome_types_are_domain_facts', () => {
  const exported = Object.keys(WorkflowSurface)
  assert.ok(exported.includes('TurnOutcome'), 'WorkflowSurface must export TurnOutcome')
  assert.ok(exported.includes('TurnDecision'), 'WorkflowSurface must export TurnDecision')
})

test('WHAT[STRUCTURED-WORKFLOW-003] Orchestrator retirement and decision state operates without resumable workflow continuation addresses', () => {
  const orchestratorWorkflowPath = path.join(
    repoRoot,
    'src/Wanxiangshu/Change/Host/OrchestratorWorkflow.fs',
  )
  const source = fs.readFileSync(orchestratorWorkflowPath, 'utf8')
  assert.equal(
    /ResumeAt|StepIndex|ContinueToken|ProgramCounter/.test(source),
    false,
    'OrchestratorWorkflow must not contain durable PC tokens',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-003] OutcomeSurface defines public vocabulary for outcome classification', () => {
  assert.equal(typeof OutcomeSurface.isTerminal, 'function')
  assert.equal(typeof OutcomeSurface.isProvisional, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-003] RECONCILE_PROGRAM_005: TurnUnknown never crosses the stable business-turn boundary', () => {
  const exported = Object.keys(ReconcileSurface)
  assert.ok(
    exported.includes('TurnOutcome'),
    'ReconcileSurface exports TurnOutcome for typed outcomes',
  )
  assert.ok(
    exported.includes('SnapshotObservation'),
    'ReconcileSurface exports SnapshotObservation for raw observations',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-003] RECONCILE_PROGRAM_007: TurnUnknown is SnapshotObservation, not TurnOutcome', () => {
  assert.ok(
    typeof ReconcileSurface.SnapshotObservation !== 'undefined',
    'SnapshotObservation must exist',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-003] SW_009_reconcile_domain_is_observation_stabilization_not_a_program', () => {
  const exported = Object.keys(ReconcileSurface)
  assert.ok(exported.includes('observe'), 'ReconcileSurface must export observe')
  assert.ok(exported.includes('decideStep'), 'ReconcileSurface must export decideStep')
})

test('WHAT[STRUCTURED-WORKFLOW-003] SW_009_recovery_surface_drives_ordinary_workflow_entrypoints', () => {
  const exported = Object.keys(RecoveryReentrySurface)
  assert.ok(
    exported.includes('reenterWorkflow'),
    'RecoveryReentrySurface must export reenterWorkflow',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-003] SW_009_change_seam_has_no_recovery_control_token_dispatcher', () => {
  const exported = Object.keys(RecoveryReentrySurface)
  assert.equal(
    exported.includes('dispatchControlToken'),
    false,
    'RecoveryReentrySurface must not export dispatchControlToken',
  )
})
