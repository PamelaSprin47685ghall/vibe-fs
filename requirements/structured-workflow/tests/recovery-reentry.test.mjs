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
import { RULES, scanText } from '../../../scripts/checks/p0-recovery-join.mjs'

const ROOT = new URL('../../../', import.meta.url).pathname
const readSrc = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const PROVIDER_RECOVERY_GATE_FIXTURES = [
  {
    id: 'provider-request-kind-owner',
    file: 'src/Wanxiangshu/Context/Prefix/Candidate.fs',
    source: 'type ProviderRequestKind = WorkMain | BloggerMain | BloggerSquash',
  },
  {
    id: 'provider-recovery-role-classification',
    file: 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs',
    source: 'let classify turn = match turn.Role with | Role.Blogger -> BloggerMain | _ -> WorkMain',
  },
  {
    id: 'provider-recovery-error-string-classification',
    file: 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Ledger.fs',
    source: 'let classify error = if error.Contains("rate limit") then BloggerSquash else BloggerMain',
  },
  {
    id: 'no-active-run-continues-recovery',
    file: 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/ConfirmedFailurePort.fs',
    source: '| ConfirmedFailureOutcome.NoActiveRun -> RecoveryAdmission.ContinueRecovery',
  },
  {
    id: 'provider-recovery-time-control',
    file: 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs',
    source: 'do! Task.Delay(TimeSpan.FromSeconds 1.)',
  },
  {
    id: 'provider-recovery-process-local-success',
    file: 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Ledger.fs',
    source: 'if recoveryFlight.ContainsKey sessionId then return Ok ConfirmedFailureOutcome.RecoveryAdvanced',
  },
  {
    id: 'old-fallback-surface-import',
    file: 'src/Wanxiangshu/Repository/Programming/Js/ProviderRecovery.fs',
    source: 'open Wanxiangshu.Participant.Provider.Attempt.Fallback.HandleSurface',
  },
  {
    id: 'old-fallback-surface-compile-entry',
    file: 'src/Wanxiangshu/Wanxiangshu.fsproj',
    source: '<Compile Include="Participant\\Provider\\Attempt\\Fallback\\Surface.fs" />',
  },
  {
    id: 'confirmed-failure-outcome-contract',
    file: 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/ConfirmedFailurePort.fs',
    source: [
      'type ConfirmedFailureOutcome =',
      '    | RecoveryAdvanced of RecoveryOpportunity',
      '    | RecoveryExhausted',
      '    | AlreadyRecorded',
      '    | NoActiveRun',
      '    | RetryScheduled',
      'type ConfirmedFailurePort = SessionId -> ProviderRunIdentity -> string -> Task<Result<ConfirmedFailureOutcome, string>>',
    ].join('\n'),
  },
  {
    id: 'workflow-confirmed-failure-exhaustive',
    file: 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs',
    source: 'let settle = function | ConfirmedFailureOutcome.RecoveryAdvanced opportunity -> opportunity',
  },
  {
    id: 'workflow-main-session-failure-owner',
    file: 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs',
    source: [
      'let ownerSessionId = mainSessionOfBloggerProjection projection turn.SessionId |> Option.defaultValue turn.SessionId',
      'FallbackLedger.recordAuthorizedFailure durable turn.SessionId authorization error',
    ].join('\n'),
  },
  {
    id: 'interaction-repair-main-session-failure-owner',
    file: 'src/Wanxiangshu/Interaction/Repair/InteractionRepair.fs',
    source: 'FallbackLedger.recordAuthorizedFailure journal turn.SessionId authorization reason',
  },
]

test('WHAT[STRUCTURED-WORKFLOW-003] SW_009_provider_recovery_gate_has_permanent_red_fixtures', () => {
  for (const fixture of PROVIDER_RECOVERY_GATE_FIXTURES) {
    const hits = scanText(fixture.source, fixture.file)
    assert.ok(
      hits.some(({ id }) => id === fixture.id),
      `${fixture.id} must reject its synthetic regression; got ${hits.map(({ id }) => id).join(', ')}`,
    )
  }
})

test('WHAT[STRUCTURED-WORKFLOW-003] SW_009_provider_recovery_rules_are_production_scoped', () => {
  for (const { id } of PROVIDER_RECOVERY_GATE_FIXTURES) {
    const rule = RULES.find((candidate) => candidate.id === id)
    assert.ok(rule, `missing provider recovery production rule ${id}`)
    assert.ok(rule.fileHint || rule.pathHint, `${id} must carry a narrow production file/path hint`)
  }
})

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
