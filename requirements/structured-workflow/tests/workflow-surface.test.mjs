// requirements/structured-workflow/tests/workflow-surface.test.mjs
//
// Structured-workflow positive surface: business programs are direct CE
// workflows (DSL-001 / FLOW-001 / ARCH-001), their exports are the story
// entrypoints — never a stored Stage/Phase/NextAction program counter
// (DSL-002 / ARCH-008). Execution/Agent/Errors + Foundation/Outcome are pure
// domain fact types consumed by those CE programs,
// not control-state tags.
//
// The registered Foundation/OutcomeSurface is the owner contract for outcome
// vocabulary. Workflow entrypoint existence is proved via source-tree
// inspection — build-verification (guide-contract.test.mjs) proves the emitted
// modules load and export callable functions.

import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

import * as outcomeSurface from '../../../dist/Foundation/OutcomeSurface.js'

const ROOT = new URL('../../../', import.meta.url).pathname
const readSrc = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[STRUCTURED-WORKFLOW-001] SW_001_workflow_entrypoints_are_the_exported_surface', () => {
  // Source-tree proof: each workflow module defines its named entrypoint as a
  // `let` — the direct-CE contract (STRUCTURED-WORKFLOW-001). Build-verification
  // (guide-contract.test.mjs) proves the emitted modules load and the
  // entrypoints are callable.
  const entrypoints = [
    ['src/Wanxiangshu/Mission/Manager/Workflow.fs', 'observe'],
    ['src/Wanxiangshu/Mission/Manager/Workflow.fs', 'observeIdle'],
    ['src/Wanxiangshu/Mission/Review/Judgement/Workflow.fs', 'observe'],
    ['src/Wanxiangshu/Composition/Turn/Workflow.fs', 'observe'],
  ]
  const missing = []
  for (const [file, name] of entrypoints) {
    const source = readSrc(file)
    if (!new RegExp(`\\blet(?: rec)?(?: private)? ${name}\\b`).test(source)) {
      missing.push(`${file}: ${name}`)
    }
  }
  assert.deepEqual(missing, [], `workflow entrypoints must exist in production source: ${missing.join('; ')}`)
})

test('WHAT[STRUCTURED-WORKFLOW-003] SW_002_workflow_modules_export_no_program_counter_shaped_names', () => {
  // DSL-002 / ARCH-008: the direct workflow owner exposes story entrypoints,
  // never a stored business stage. Source-tree proof: no Stage/Phase/NextAction
  // let bindings in the workflow modules.
  const workflowFiles = [
    'src/Wanxiangshu/Mission/Manager/Workflow.fs',
    'src/Wanxiangshu/Mission/Review/Judgement/Workflow.fs',
    'src/Wanxiangshu/Composition/Turn/Workflow.fs',
  ]
  const programCounter = /\b(?:let|type)\s+\w*(?:Stage|Phase|NextAction|CurrentStage|RunState)\b/
  const bad = []
  for (const file of workflowFiles) {
    const source = readSrc(file)
    if (programCounter.test(source)) bad.push(file)
  }
  assert.deepEqual(bad, [], `workflow modules must not define program-counter-shaped names: ${bad.join('; ')}`)
})

test('WHAT[STRUCTURED-WORKFLOW-003] SW_003_domain_flow_and_outcome_types_are_domain_facts', () => {
  // Agent errors remain domain facts. Companion has no generic context/error
  // workflow vocabulary: direct durable and transform owners carry its operations.
  const agentErrors = readSrc('src/Wanxiangshu/Execution/Agent/Errors.fs')
  assert.match(agentErrors, /\btype AgentContext\b/, 'Execution/Agent/Errors must define AgentContext')
  assert.match(agentErrors, /\btype AgentError\b/, 'Execution/Agent/Errors must define AgentError')

  assert.equal(
    existsSync(join(ROOT, 'src/Wanxiangshu/Context/Companion/Errors.fs')),
    false,
    'Context/Companion/Errors.fs must stay deleted with its generic flow facade',
  )

  const companionOwnerFiles = [
    'src/Wanxiangshu/Context/Companion/Model.fs',
    'src/Wanxiangshu/Context/Companion/Runtime.fs',
    'src/Wanxiangshu/Context/Companion/Host.fs',
    'src/Wanxiangshu/Context/Companion/HostBlogger.fs',
    'src/Wanxiangshu/Context/Companion/JournalPort.fs',
    'src/Wanxiangshu/Context/Companion/Transform.fs',
  ]
  const companionOwners = companionOwnerFiles.map(readSrc).join('\n')
  assert.doesNotMatch(companionOwners, /\b(?:CompanionProgram|CompanionContext|CompanionError)\b/)
  assert.match(companionOwners, /\bICompanionDurablePort\b/)
  assert.match(companionOwners, /\bapplyCompanionForOrdinaryMaterial\b/)

  // AgentRunResult is the completion payload of a successful agent run
  // (EXEC-006): typed physical facts plus terminal output, validated by
  // IsValid. No transport parts, no stage latch.
  const outcome = readSrc('src/Wanxiangshu/Foundation/Outcome.fs')
  assert.match(outcome, /\btype AgentRunResult\b/, 'Foundation/Outcome must define AgentRunResult')
  assert.match(outcome, /\btype AgentRunFailure\b/, 'Foundation/Outcome must define AgentRunFailure')
  // EXEC-006: IsValid rejects empty terminal text — the JS-native surface
  // exposes this without the test touching Fable record internals.
  assert.equal(outcomeSurface.isValidAgentRunResult(''), false)
  assert.equal(outcomeSurface.isValidAgentRunResult('  '), false)
  assert.equal(outcomeSurface.isValidAgentRunResult('terminal output'), true)

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
