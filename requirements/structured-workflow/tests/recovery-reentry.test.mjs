// requirements/structured-workflow/tests/recovery-reentry.test.mjs
//
// DSL-004 / FLOW-005 / ARCH-005: recovery re-enters ordinary workflow from
// durable facts. There is no continuation-pointer / program-counter restore
// surface anywhere in the reconcile or recovery program path — the reconcile
// domain is an observation-stabilization boundary (ce-temporal-ownership §17),
// and recovery drives the same named workflow entrypoints the live path uses.
//
// The registered ReconcileSurface is the owner contract for the first test.
// The second test proves the source-tree invariant: recovery workflow modules
// export named entrypoints (not stored positions), verified by reading the
// production source — build-verification (guide-contract.test.mjs) proves the
// emitted modules load and export callable functions.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import * as reconcile from '../../../dist/Composition/Turn/ReconcileSurface.js'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const ROOT = new URL('../../../', import.meta.url).pathname
const readSrc = (rel) => readFileSync(join(ROOT, rel), 'utf8')

test('WHAT[STRUCTURED-WORKFLOW-003] SW_009_reconcile_domain_is_observation_stabilization_not_a_program', () => {
  // The registered ReconcileSurface is the owner contract: callers observe
  // bounded reread + publish decisions, never emitted union metadata.
  for (const n of [
    'decideStep',
    'decisionName',
    'publishDecision',
    'isTerminalOutcome',
    'clearsContinuationCandidate',
    'consumeKey',
    'clearProvisional',
  ]) {
    assert.equal(typeof reconcile[n], 'function', `ReconcileSurface must export ${n}`)
  }

  // No second-runtime restore surface: no continuation-pointer restore, no
  // program AST, no interpreter. (RECONCILE_PROGRAM_006 pins the same
  // absence for Command/Reply/Trace exports.)
  for (const n of [
    'RestoreContinuation',
    'ResumeProgram',
    'ReplayProgram',
    'ProgramNode',
    'TraceInterpreter',
    'CommandBus',
    'StepAst',
    'materializePass',
    'interpretWith',
    'ProtocolMismatch',
  ]) {
    assert.equal(n in reconcile, false, `ReconcileSurface must not export ${n}`)
  }
})

test('WHAT[STRUCTURED-WORKFLOW-003] SW_009_recovery_surface_drives_ordinary_workflow_entrypoints', () => {
  // Recovery is a permit-gated re-entry into the same named workflows the
  // live path uses (ARCH-005 / ce-temporal-ownership §15–17): the
  // SessionRecoveryWorkflow entry, the provider recovery vocabulary, and the
  // thin per-context TurnWorkflow router. None of them is a stored position.
  //
  // Source-tree proof: each workflow module defines its named entrypoint as a
  // `let` — the direct-CE contract (STRUCTURED-WORKFLOW-001). Build-verification
  // (guide-contract.test.mjs) proves the emitted modules load and the
  // entrypoints are callable.
  const entrypoints = [
    ['src/Wanxiangshu/Execution/Session/Recovery/Workflow.fs', 'recoverFamilyDirect'],
    ['src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs', 'continueAfterConfirmedFailure'],
    ['src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs', 'awaitRecoveryMaterial'],
    ['src/Wanxiangshu/Composition/Turn/Workflow.fs', 'observe'],
    ['src/Wanxiangshu/Mission/Manager/Workflow.fs', 'observe'],
    ['src/Wanxiangshu/Mission/Manager/Workflow.fs', 'observeIdle'],
    ['src/Wanxiangshu/Mission/Review/Judgement/Workflow.fs', 'observe'],
  ]
  const missing = []
  for (const [file, name] of entrypoints) {
    const source = readSrc(file)
    if (!new RegExp(`\\blet(?: rec)?(?: private)? ${name}\\b`).test(source)) {
      missing.push(`${file}: ${name}`)
    }
  }
  assert.deepEqual(missing, [], `recovery workflow entrypoints must exist in production source: ${missing.join('; ')}`)
})

test('WHAT[STRUCTURED-WORKFLOW-003] SW_009_change_seam_has_no_recovery_control_token_dispatcher', () => {
  // OBL-002: the Change seam must not re-introduce a JobRecoveryAction
  // control-token type, a recoveryAction producer, a resumeFromDurableFacts
  // interpreter, or any equivalent NextAction dispatcher. Recovery re-enters
  // the ordinary CE workflow by matching independent durable facts directly.
  const forbidden = [
    'src/Wanxiangshu/Change/Projection.fs',
    'src/Wanxiangshu/Change/Program.fs',
    'src/Wanxiangshu/Change/Surface.fs',
    'src/Wanxiangshu/Mission/Manager/FinalitySurface.fs',
  ]
  const symbols = [
    /\bJobRecoveryAction\b/,
    /\brecoveryAction\b/,
    /\bresumeFromDurableFacts\b/,
    /\brecoveryFromProgress\b/,
    /\brecoveryFor\b/,
    /\bactionObject\b/,
    /\brecoveryActionView\b/,
  ]
  const violations = []
  for (const file of forbidden) {
    const source = readSrc(file)
    for (const pattern of symbols) {
      if (pattern.test(source)) {
        violations.push(`${file}: ${pattern.source}`)
      }
    }
  }
  assert.deepEqual(violations, [], `Change seam must not re-introduce recovery control tokens: ${violations.join('; ')}`)
})
