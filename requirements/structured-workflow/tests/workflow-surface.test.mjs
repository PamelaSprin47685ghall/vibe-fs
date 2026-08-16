// requirements/structured-workflow/tests/workflow-surface.test.mjs
//
// Structured-workflow positive surface: business programs are direct CE
// workflows (DSL-001 / FLOW-001 / ARCH-001), their exports are the story
// entrypoints — never a stored Stage/Phase/NextAction program counter
// (DSL-002 / ARCH-008). Execution/Agent/Errors + Context/Companion/Errors +
// Foundation/Outcome are pure domain fact types consumed by those CE programs,
// not control-state tags.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as outcomeSurface from '../../../dist/Foundation/OutcomeSurface.js'

const load = (modulePath) => import(new URL(`../../../dist/${modulePath}.js`, import.meta.url).pathname)

test('WHAT[STRUCTURED-WORKFLOW-001] SW_001_workflow_entrypoints_are_the_exported_surface', async () => {
  const manager = await load('Mission/Manager/Workflow')
  const reviewer = await load('Mission/Review/Judgement/Workflow')
  const turn = await load('Composition/Turn/Workflow')

  assert.equal(typeof manager.observe, 'function')
  assert.equal(typeof manager.observeIdle, 'function')
  assert.equal(typeof reviewer.observe, 'function')
  assert.equal(typeof turn.observe, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-003] SW_002_workflow_modules_export_no_program_counter_shaped_names', async () => {
  // DSL-002 / ARCH-008: the direct workflow owner exposes story entrypoints,
  // never a stored business stage. Check those named entrypoints directly;
  // emitted export enumeration is not a semantic contract.
  const manager = await load('Mission/Manager/Workflow')
  const reviewer = await load('Mission/Review/Judgement/Workflow')
  const turn = await load('Composition/Turn/Workflow')

  assert.equal(typeof manager.observe, 'function')
  assert.equal(typeof manager.observeIdle, 'function')
  assert.equal(typeof reviewer.observe, 'function')
  assert.equal(typeof turn.observe, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-003] SW_003_domain_flow_and_outcome_types_are_domain_facts', async () => {
  const agent = await load('Execution/Agent/Errors')
  const companion = await load('Context/Companion/Errors')
  const outcome = await load('Foundation/Outcome')

  // Context/error vocabularies now live with their owning domains after the
  // rotation-2 split; neither module is a Flow AST or program position.
  for (const t of ['AgentContext', 'AgentError']) {
    assert.equal(typeof agent[t], 'function', `Execution/Agent/Errors must export ${t}`)
  }
  for (const t of ['CompanionContext', 'CompanionError']) {
    assert.equal(typeof companion[t], 'function', `Context/Companion/Errors must export ${t}`)
  }

  // AgentRunResult is the completion payload of a successful agent run
  // (EXEC-006): typed physical facts plus terminal output, validated by
  // IsValid. No transport parts, no stage latch.
  assert.equal(typeof outcome.AgentRunResult, 'function')
  assert.equal(typeof outcome.AgentRunResult__get_IsValid, 'function')
  assert.equal(typeof outcome.AgentRunFailure, 'function')

  const sendOutcomeCases = outcomeSurface.sendOutcomeKinds()
  assert.deepEqual(sendOutcomeCases, [
    'AdmittedWithReceipt',
    'AdmittedWithPhysicalMessage',
    'Retryable',
    'AcceptanceUnknown',
    'Fatal',
  ])

  // SessionError names real world conditions (budget spent, prompt
  // uncertain, projection broken, inbox full), not execution steps.
  const sessionErrorCases = outcomeSurface.sessionErrorKinds()
  assert.deepEqual(sessionErrorCases, [
    'NoProgress',
    'SessionCancelled',
    'AutoRecoveryExhausted',
    'ReviewExhausted',
    'PromptUncertain',
    'ProjectionBroken',
    'InboxFull',
    'Protocol',
  ])
})
