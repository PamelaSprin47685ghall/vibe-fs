// requirements/structured-workflow/tests/recovery-reentry.test.mjs
//
// DSL-004 / FLOW-005 / ARCH-005: recovery re-enters ordinary workflow from
// durable facts. There is no continuation-pointer / program-counter restore
// surface anywhere in the reconcile or recovery program path — the reconcile
// domain is an observation-stabilization boundary (ce-temporal-ownership §17),
// and recovery drives the same named workflow entrypoints the live path uses.

import assert from 'node:assert/strict'
import test from 'node:test'

const load = (modulePath) => import(new URL(`../../../dist/${modulePath}.js`, import.meta.url).pathname)

/** Every emitted name, minus the reflection metadata Fable adds per type. */
const surfaceOf = (mod) => Object.keys(mod).filter((name) => !name.endsWith('_$reflection'))

test('WHAT[STRUCTURED-WORKFLOW-009] SW_009_reconcile_domain_is_observation_stabilization_not_a_program', async () => {
  const mod = await load('Composition/Turn/Program')

  // The pure reconcile surface is bounded reread + publish decisions
  // (HOST-004): decision from evidence, consume keys, terminal classification.
  for (const n of [
    'decideStep',
    'decisionName',
    'publishDecision',
    'isTerminalOutcome',
    'clearsContinuationCandidate',
    'consumeKey',
    'clearProvisional',
  ]) {
    assert.equal(typeof mod[n], 'function', `ReconcileProgram must export ${n}`)
  }

  // No second-runtime restore surface: no continuation-pointer restore, no
  // program AST, no interpreter. (RECONCILE_PROGRAM_006 pins the same
  // absence for Command/Reply/Trace exports.)
  const names = surfaceOf(mod)
  const forbidden = names.filter((n) =>
    /(RestoreContinuation|ResumeProgram|ReplayProgram|ProgramNode|TraceInterpreter|CommandBus|StepAst|materializePass|interpretWith|ProtocolMismatch)/.test(n),
  )
  assert.deepEqual(forbidden, [], 'ReconcileProgram must not export continuation-restore / interpreter shapes')
})

test('WHAT[STRUCTURED-WORKFLOW-009] SW_009_recovery_surface_drives_ordinary_workflow_entrypoints', async () => {
  // Recovery is a permit-gated re-entry into the same named workflows the
  // live path uses (ARCH-005 / ce-temporal-ownership §15–17): the
  // SessionRecoveryWorkflow entry, the provider recovery vocabulary, and the
  // thin per-context TurnWorkflow router. None of them is a stored position.
  const sessionRecovery = await load('Execution/Session/Recovery/Workflow')
  assert.equal(typeof sessionRecovery.recoverFamilyDirect, 'function')

  const providerRecovery = await load('Participant/Provider/Attempt/Fallback/Workflow')
  for (const n of ['continueAfterConfirmedFailure', 'continueAfterLoopKill', 'awaitRecoveryMaterial']) {
    assert.equal(typeof providerRecovery[n], 'function', `ProviderRecoveryWorkflow must export ${n}`)
  }

  const turn = await load('Composition/Turn/Workflow')
  assert.equal(typeof turn.observe, 'function')

  const manager = await load('Mission/Manager/Workflow')
  assert.equal(typeof manager.observe, 'function')
  const reviewer = await load('Mission/Review/Judgement/Workflow')
  assert.equal(typeof reviewer.observe, 'function')
})
