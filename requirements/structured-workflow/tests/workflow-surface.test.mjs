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

const load = (modulePath) => import(new URL(`../../../dist/${modulePath}.js`, import.meta.url).pathname)

/** Every emitted name, minus the reflection metadata Fable adds per type. */
const surfaceOf = (mod) => Object.keys(mod).filter((name) => !name.endsWith('_$reflection'))

test('WHAT[STRUCTURED-WORKFLOW-001] SW_001_workflow_entrypoints_are_the_exported_surface', async () => {
  const manager = await load('Mission/Manager/Workflow')
  const reviewer = await load('Mission/Review/Judgement/Workflow')
  const turn = await load('Composition/Turn/Workflow')

  assert.deepEqual(surfaceOf(manager).sort(), ['observe', 'observeIdle'])
  assert.deepEqual(surfaceOf(reviewer).sort(), ['observe'])
  assert.deepEqual(surfaceOf(turn).sort(), ['observe'])

  assert.equal(typeof manager.observe, 'function')
  assert.equal(typeof manager.observeIdle, 'function')
  assert.equal(typeof reviewer.observe, 'function')
  assert.equal(typeof turn.observe, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-003] SW_002_workflow_modules_export_no_program_counter_shaped_names', async () => {
  // DSL-002 / ARCH-008: a stored business stage would surface here as an
  // exported tag. The workflow modules must expose only story entrypoints.
  const programCounterShape = /(Stage|Phase|NextAction|Disposition|ProgramCounter|ProgramStep)$/
  for (const modulePath of [
    'Mission/Manager/Workflow',
    'Mission/Review/Judgement/Workflow',
    'Composition/Turn/Workflow',
  ]) {
    const names = surfaceOf(await load(modulePath))
    const hits = names.filter((n) => programCounterShape.test(n))
    assert.deepEqual(hits, [], `${modulePath} must not export program-counter-shaped names`)
  }
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

  // SendOutcome cases are physical/domain facts about the Host admission
  // result (PROMPT-005): two admitted kinds (receipt vs real message id),
  // retryable, acceptance-unknown, fatal. None is a program position.
  const sendOutcomeCases = Object.create(outcome.Outcome_SendOutcome.prototype).cases()
  assert.deepEqual(sendOutcomeCases, [
    'AdmittedWithReceipt',
    'AdmittedWithPhysicalMessage',
    'Retryable',
    'AcceptanceUnknown',
    'Fatal',
  ])

  // SessionError cases name real world conditions (budget spent, prompt
  // uncertain, projection broken, inbox full), not execution steps.
  const sessionErrorCases = Object.create(outcome.Outcome_SessionError.prototype).cases()
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
