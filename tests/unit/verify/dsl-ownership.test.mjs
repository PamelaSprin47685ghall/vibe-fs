/**
 * FLOW-006: dsl-ownership gate must go red on each forbidden pattern.
 * Synthetic source only — never mutates production trees.
 */
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import {
  FORBIDDEN,
  GATE_NAMES,
  HOST_BOUNDARY_OPEN_BASENAMES,
  DSL_CLASSES,
  LARGE_DU_THRESHOLD,
  evaluateThreshold,
  isHostBoundaryOpenPath,
  scanFiles,
  scanLargeDus,
  scanText,
} from '../../../scripts/checks/dsl-ownership.mjs'

const readFixture = (name) => readFileSync(new URL(`./fixtures/${name}`, import.meta.url), 'utf8')

const NEGATIVES = [
  {
    gate: 'mutable',
    source: ['module Sample', 'let mutable counter = 0'].join('\n'),
    line: 2,
  },
  {
    gate: 'flow-lift',
    source: ['module Sample', 'let escape t = Flow.create t'].join('\n'),
    line: 2,
  },
  {
    gate: 'second-runtime-protocol',
    source: ['module Sample', 'type WorkflowCommand =', '    | ReadSnapshot'].join('\n'),
    line: 2,
  },
  {
    gate: 'business-interpreter',
    source: ['module Sample', 'module WorkflowInterpreter =', '    let run program = program'].join('\n'),
    line: 2,
  },
  {
    gate: 'infrastructure-leak',
    source: ['module Sample', 'open Wanxiangshu.Infrastructure'].join('\n'),
    line: 2,
  },
  {
    gate: 'program-counter',
    source: ['module Sample', 'type Flags = { Dirty: bool }'].join('\n'),
    line: 2,
  },
  {
    gate: 'program-counter',
    source: ['module Sample', 'type Runtime = { CurrentStage: bool }'].join('\n'),
    line: 2,
  },
  {
    gate: 'bool-loop',
    file: 'Agent/Negative_bool_loop.fs',
    source: ['module Sample', 'let mutable armed = false', 'let mutable spent = false', 'while not done do', '    ()'].join('\n'),
    line: 2,
  },
  {
    gate: 'dup-cases',
    file: 'Session/ChildRecovery.fs',
    source: ['module Sample', 'type Alpha =', '    | First', '    | Second', 'type ChildResolution =', '    | First', '    | Second'].join('\n'),
    line: 2,
  },
  {
    gate: 'bool-loop',
    file: 'src/Wanxiangshu/Process/Negative_bool_loop.fs',
    source: ['module Sample', 'let mutable a = false', 'let mutable b = false', 'while x do', '    ()'].join('\n'),
    line: 2,
  },
  {
    gate: 'behaviour-bool',
    source: ['module Sample', 'type State = { HasPendingCompletion: bool }'].join('\n'),
    line: 2,
  },
]

const CLEAN = [
  'module Sample',
  'open Wanxiangshu.Domain',
  'let run operation = task {',
  '    let! result = operation ()',
  '    return result',
  '}',
].join('\n')

test('DSL_OWNERSHIP_large_du_without_class_annotation_is_reported', () => {
  const src = ['module Sample', 'type Big =', '    | C01', '    | C02', '    | C03', '    | C04', '    | C05', '    | C06', '    | C07', '    | C08', '    | C09', '    | C10'].join('\n')
  const reported = scanLargeDus(src, 'Agent/Large.fs')
  assert.equal(reported.length, 1)
  assert.equal(reported[0].name, 'Big')
  assert.equal(reported[0].cases, 10)
})

test('DSL_OWNERSHIP_large_du_with_class_annotation_is_clean', () => {
  const src = [
    'module Sample',
    '/// DSL-class: Vocabulary — fixed catalog.',
    'type Big =',
    '    | C01',
    '    | C02',
    '    | C03',
    '    | C04',
    '    | C05',
    '    | C06',
    '    | C07',
    '    | C08',
    '    | C09',
    '    | C10',
  ].join('\n')
  assert.deepEqual(scanLargeDus(src, 'Agent/Large.fs'), [])
})

test('DSL_OWNERSHIP_large_du_class_annotation_across_attribute_is_clean', () => {
  // Roles.fs shape: `/// DSL-class:` separated from `type` by a
  // `[<RequireQualifiedAccess>]` attribute must still be matched (the lookback
  // skips attribute/blank lines while collecting `///` doc lines).
  const src = [
    'module Sample',
    '/// DSL-class: Vocabulary — fixed catalog.',
    '[<RequireQualifiedAccess>]',
    'type Big =',
    '    | C01',
    '    | C02',
    '    | C03',
    '    | C04',
    '    | C05',
    '    | C06',
    '    | C07',
    '    | C08',
    '    | C09',
    '    | C10',
  ].join('\n')
  assert.deepEqual(scanLargeDus(src, 'Agent/Large.fs'), [])
})

test('DSL_OWNERSHIP_large_du_class_annotation_across_attribute_and_blank_is_clean', () => {
  // Attribute plus blank lines between doc and `type` are skipped too.
  const src = [
    'module Sample',
    '',
    '/// DSL-class: Vocabulary — fixed catalog.',
    '[<RequireQualifiedAccess>]',
    '',
    'type Big =',
    '    | C01',
    '    | C02',
    '    | C03',
    '    | C04',
    '    | C05',
    '    | C06',
    '    | C07',
    '    | C08',
    '    | C09',
    '    | C10',
  ].join('\n')
  assert.deepEqual(scanLargeDus(src, 'Agent/Large.fs'), [])
})

test('DSL_OWNERSHIP_large_du_unclassified_still_reported_behind_attribute', () => {
  // A large DU with no DSL-class annotation is still reported even when an
  // attribute sits between the doc area and the type declaration.
  const src = [
    'module Sample',
    '/// A doc comment that is not a DSL-class annotation.',
    '[<RequireQualifiedAccess>]',
    'type Big =',
    '    | C01',
    '    | C02',
    '    | C03',
    '    | C04',
    '    | C05',
    '    | C06',
    '    | C07',
    '    | C08',
    '    | C09',
    '    | C10',
  ].join('\n')
  const reported = scanLargeDus(src, 'Agent/Large.fs')
  assert.equal(reported.length, 1)
  assert.equal(reported[0].name, 'Big')
  assert.equal(reported[0].cases, 10)
})

test('DSL_OWNERSHIP_small_du_is_never_reported', () => {
  const src = ['module Sample', 'type Small =', '    | A', '    | B'].join('\n')
  assert.deepEqual(scanLargeDus(src, 'Agent/Small.fs'), [])
})

test('DSL_OWNERSHIP_exports_nine_named_gates', () => {
  assert.deepEqual(GATE_NAMES, [
    'mutable',
    'flow-lift',
    'second-runtime-protocol',
    'business-interpreter',
    'infrastructure-leak',
    'program-counter',
    'behaviour-bool',
    'bool-loop',
    'dup-cases',
    'registry-joint-branch',
  ])
  assert.equal(FORBIDDEN.length, 10)
})

for (const sample of NEGATIVES) {
  if (sample.gate === 'bool-loop' || sample.gate === 'dup-cases') continue
  test(`DSL_OWNERSHIP_negative_${sample.gate}_goes_red`, () => {
    const file = sample.file ?? `Agent/Negative_${sample.gate}.fs`
    const hits = scanText(sample.source, file)
    const ofGate = hits.filter((v) => v.gate === sample.gate)
    assert.ok(ofGate.length >= 1, `expected gate ${sample.gate} to fire`)
    assert.equal(ofGate[0].line, sample.line)
    assert.equal(ofGate[0].file, file)
  })
}

for (const sample of NEGATIVES.filter((s) => s.gate === 'bool-loop' || s.gate === 'dup-cases')) {
  test(`DSL_OWNERSHIP_negative_${sample.gate}_goes_red`, () => {
    const hits = scanFiles([{ file: sample.file, text: sample.source }])
    const ofGate = hits.filter((v) => v.gate === sample.gate)
    if (sample.file.endsWith('ChildRecovery.fs')) {
      // DUP_CASES_EXEMPT registers ChildRecovery.fs:ChildResolution; a DU with
      // that exact basename:name must not fire.
      assert.equal(ofGate.length, 0, 'registered exemption does not fire')
    } else {
      assert.ok(ofGate.length >= 1, `expected gate ${sample.gate} to fire`)
      assert.equal(ofGate[0].line, sample.line)
    }
  })
}

test('DSL_OWNERSHIP_clean_source_stays_green', () => {
  const hits = scanText(CLEAN, 'Domain/Clean.fs')
  assert.deepEqual(hits, [])
})

test('DSL_OWNERSHIP_domain_pending_evidence_is_not_behaviour_bool', () => {
  const source = [
    'module Sample',
    'type Witness = | PerfectPending of unit | StillPending of bool | ConflictPending of unit',
    'let recoveryBudgetSpent claim = true',
    'let tryTakePending cell = None',
    'let isPerfectPending w = true',
  ].join('\n')
  const hits = scanText(source, 'Domain/Clean.fs')
  assert.deepEqual(hits, [])
})

test('DSL_OWNERSHIP_verb_named_function_ending_Pending_is_not_behaviour_bool', () => {
  // A verb-named function that ends in a nominal suffix is a pure operation,
  // not a stored stage latch. `clearStalePending` / `tryClaimStartupProbe` are
  // names of work being performed, not fields recording "which step we are at".
  const source = [
    'module Sample',
    'let clearStalePending agentId = ()',
    'let tryClaimStartupProbe () = true',
    'let failPending entries reason = ()',
    'let takePending supervisor id = []',
    'let hasPendingActivation journal sessionId = false',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/Infrastructure/OpenCode/Orchestration/Host.fs')
  assert.ok(
    !hits.some((h) => h.gate === 'behaviour-bool'),
    'verb-named functions ending in Pending/Probe are pure operations, not stage latch names',
  )
})

test('DSL_OWNERSHIP_physical_pending_latch_and_estimate_fields_are_not_behaviour_bool', () => {
  // Cat-C false positives: physical abort set, runtime estimate field, recovery
  // probe type alias, and fold-rejection tokens that merely end in Already/Pending.
  const source = [
    'module Sample',
    'let abortPending = HashSet<PtyId>()',
    'type Request = { EstimatedRunningSeconds: float }',
    'type RecoveryStageProbe = unit -> unit',
    'type GuardNudgeOutcome = | AlreadyOutstanding | Sent',
    'type PendingSeal = { ReviewerId: string }',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/Infrastructure/OpenCode/Tools/ExecutorTool.fs')
  assert.ok(
    !hits.some((h) => h.gate === 'behaviour-bool'),
    'physical Pending/Already/Running names must not fire behaviour-bool',
  )
})

test('DSL_OWNERSHIP_field_named_HasPendingCompletion_still_fires_behaviour_bool', () => {
  // The residual exact-name block is the authority for stage latches: a
  // stored boolean completion slot must keep firing.
  const source = ['module Sample', 'type State = { HasPendingCompletion: bool }'].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/Session/Sample.fs')
  assert.ok(
    hits.some((h) => h.gate === 'behaviour-bool'),
    'a stored HasPendingCompletion slot must still fire behaviour-bool',
  )
})

test('DSL_OWNERSHIP_pascal_member_Pending_still_fires_behaviour_bool', () => {
  // A PascalCase property named as a stage latch (CompactionProbePending) is a
  // stored control slot and must fire even though verb-named functions are now
  // allowed.
  const source = ['module Sample', 'type S() =', '    member _.CompactionProbePending = true'].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/Session/Sample.fs')
  assert.ok(
    hits.some((h) => h.gate === 'behaviour-bool'),
    'a PascalCase stage-latch property must still fire behaviour-bool',
  )
})

test('DSL_OWNERSHIP_business_stage_bool_suffix_still_fires_behaviour_bool', () => {
  // Residual suffix must still catch true business stage bools that are not
  // domain evidence / physical allowlist entries.
  const source = ['module Sample', 'type Flags = { isRunning: bool; repairSpent: bool }'].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/Session/Sample.fs')
  assert.ok(
    hits.some((h) => h.gate === 'behaviour-bool'),
    'business *Running/*Spent stage bools must still fire behaviour-bool',
  )
})

test('DSL_OWNERSHIP_qualified_infrastructure_reference_is_leak_outside_infra', () => {
  const source = [
    'module Sample',
    'let prompts () = Wanxiangshu.Infrastructure.Resources.RuntimeResources.current().Prompts',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/Tools/StaticTools.fs')
  assert.ok(
    hits.some((h) => h.gate === 'infrastructure-leak'),
    'FQN Infrastructure reference outside Infrastructure/Process/Host-boundary must fire',
  )
})

test('DSL_OWNERSHIP_qualified_process_reference_is_leak_outside_infra', () => {
  const source = [
    'module Sample',
    'let run () = Wanxiangshu.Process.ProcessRunner.run cmd est ctx ct',
  ].join('\n')
  const hits = scanText(source, 'src/Wanxiangshu/Tools/StaticTools.fs')
  assert.ok(
    hits.some((h) => h.gate === 'infrastructure-leak'),
    'FQN Process reference outside Infrastructure/Process/Host-boundary must fire',
  )
})

test('DSL_OWNERSHIP_namespace_OpenCode_declaration_is_not_infrastructure_leak', () => {
  // A `namespace Wanxiangshu.OpenCode` line declares the module's own home, not
  // a dependency on the infrastructure layer — it must not fire
  // infrastructure-leak (C-class false positive).
  const source = 'namespace Wanxiangshu.OpenCode\nmodule Sample =\n    let x = 1'
  const hits = scanText(source, 'src/Wanxiangshu/Application/Reconciliation/Sample.fs')
  assert.ok(!hits.some((h) => h.gate === 'infrastructure-leak'))
})

test('DSL_OWNERSHIP_namespace_Process_declaration_is_not_infrastructure_leak', () => {
  const source = 'namespace Wanxiangshu.Process\nmodule Sample =\n    let x = 1'
  const hits = scanText(source, 'src/Wanxiangshu/Application/Reconciliation/Sample.fs')
  assert.ok(!hits.some((h) => h.gate === 'infrastructure-leak'))
})

test('DSL_OWNERSHIP_qualified_process_reference_is_clean_inside_infra', () => {
  // Infrastructure may use Process FQN without leaking across the boundary.
  const hits = scanText(
    'module Sample\nlet x = Wanxiangshu.Process.ProcessRunner.run',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/ExecutorTool.fs',
  ).filter((h) => h.gate === 'infrastructure-leak')
  assert.deepEqual(hits, [], 'Infrastructure path must stay clean for Process FQN')
})


test('DSL_OWNERSHIP_scanFiles_aggregates_entries', () => {
  const hits = scanFiles([
    { file: 'a.fs', text: NEGATIVES[0].source },
    { file: 'b.fs', text: CLEAN },
    { file: 'c.fs', text: NEGATIVES[1].source },
  ])
  const gates = hits.map((v) => v.gate)
  assert.ok(gates.includes('mutable'))
  assert.ok(gates.includes('flow-lift'))
  assert.equal(
    hits.filter((v) => v.file === 'b.fs').length,
    0,
  )
})


test('DSL_OWNERSHIP_mutable_requires_dsl_mutable_declaration', () => {
  // Domain/Session/Application/Process/Kernel.Parallel: a bare `let mutable`
  // is a violation; the preceding 1-2 lines must carry `// DSL-MUTABLE:`.
  const bare = ['module Sample', 'let scratch () =', '    let mutable acc = 0', '    acc'].join('\n')
  for (const path of [
    'src/Wanxiangshu/Domain/Sample.fs',
    'src/Wanxiangshu/Session/Sample.fs',
    'src/Wanxiangshu/Application/Sample.fs',
    'src/Wanxiangshu/Process/Sample.fs',
    'src/Wanxiangshu/Kernel/Parallel.fs',
  ]) {
    assert.ok(scanText(bare, path).some((h) => h.gate === 'mutable'), `bare mutable must fire in ${path}`)
  }

  // A DSL-MUTABLE declaration legalizes a mutable on an allowed path.
  const declared = [
    'module Sample',
    'let scratch () =',
    '    // DSL-MUTABLE: algorithm-scratch — loop accumulator',
    '    let mutable acc = 0',
    '    acc',
  ].join('\n')
  for (const path of [
    'src/Wanxiangshu/Domain/Sample.fs',
    'src/Wanxiangshu/Session/Sample.fs',
    'src/Wanxiangshu/Application/Sample.fs',
    'src/Wanxiangshu/Process/Sample.fs',
    'src/Wanxiangshu/Kernel/Parallel.fs',
  ]) {
    assert.deepEqual(scanText(declared, path), [], `declared mutable must stay green in ${path}`)
  }

  // Mutable policy is now declaration-gated for ALL production paths
  // (isMutableDeclarationAllowed = true): Agent and non-Parallel Kernel are
  // no longer categorically fail-closed. A bare mutable still fires there;
  // a precise declaration legalizes it.
  assert.ok(scanText(bare, 'src/Wanxiangshu/Agent/Sample.fs').some((h) => h.gate === 'mutable'))
  assert.ok(scanText(bare, 'src/Wanxiangshu/Kernel/Outcome.fs').some((h) => h.gate === 'mutable'))
  assert.deepEqual(
    scanText(declared, 'src/Wanxiangshu/Agent/Sample.fs'),
    [],
    'a declared mutable must stay green in Agent',
  )
  assert.deepEqual(
    scanText(declared, 'src/Wanxiangshu/Kernel/Outcome.fs'),
    [],
    'a declared mutable must stay green in Kernel',
  )
})

test('DSL_OWNERSHIP_unknown_mutable_category_is_rejected', () => {
  const source = [
    'module Sample',
    'let scratch () =',
    '    // DSL-MUTABLE: mystery — not a registered category',
    '    let mutable acc = 0',
    '    acc',
  ].join('\n')
  assert.ok(scanText(source, 'src/Wanxiangshu/Domain/Sample.fs').some((h) => h.gate === 'mutable'))
})

test('DSL_OWNERSHIP_control_state_class_is_a_program_counter', () => {
  const source = [
    'module Sample',
    '/// DSL-class: ControlState',
    'type Mode =',
    '    | A',
    '    | B',
  ].join('\n')
  assert.ok(scanText(source, 'src/Wanxiangshu/Session/Sample.fs').some((h) => h.gate === 'program-counter'))
})

test('DSL_OWNERSHIP_renamed_record_state_axes_are_reported', () => {
  const hits = scanText(readFixture('state-axes-illegal.fs'), 'src/Wanxiangshu/Domain/StateAxes.fs')
  assert.ok(
    hits.some((hit) => hit.gate === 'state-product'),
    'independent state axes must be reported without relying on field names',
  )
})

// DSL-005/007: a record carrying `mutable foo:` state fields must produce a
// state-product violation. A
// production file with this fixture is therefore rejected by the CLI gate.
test('DSL_OWNERSHIP_mutable_record_program_counter_fires_state_product', () => {
  const hits = scanText(
    readFixture('mutable-record-program-counter.fs'),
    'src/Wanxiangshu/Session/StudentRunCell.fs',
  )
  assert.ok(
    hits.some((hit) => hit.gate === 'state-product'),
    'a mutable-record field set of >= 2 independent state axes must fire state-product (DSL-005/007)',
  )
})

test('DSL_OWNERSHIP_mutable_record_program_counter_fires_mutable_record_field', () => {
  const hits = scanText(
    readFixture('mutable-record-program-counter.fs'),
    'src/Wanxiangshu/Session/StudentRunCell.fs',
  )
  assert.ok(
    hits.some((hit) => hit.gate === 'mutable-record-field'),
    'State/Handoff are business tokens and Return/Final are Session mutable fields with no physical annotation, so the mutable-record-field gate must fire',
  )
})

test('DSL_OWNERSHIP_ref_record_program_counter_fires_mutable_record_field', () => {
  const hits = scanText(
    readFixture('ref-record-program-counter.fs'),
    'src/Wanxiangshu/Session/StudentRunCell.fs',
  )
  assert.ok(
    hits.some((hit) => hit.gate === 'mutable-record-field'),
    'ref storage must not bypass the mutable-record-field gate',
  )
  assert.ok(
    hits.some((hit) => hit.gate === 'state-product'),
    'ref storage must count as an independent state axis',
  )
})

test('DSL_OWNERSHIP_joint_registry_match_with_effect_fires_registry_joint_branch', () => {
  const hits = scanText(
    readFixture('registry-joint-branch.fs'),
    'src/Wanxiangshu/Session/SyncDelegateRuntime.fs',
  )
  assert.ok(
    hits.some((hit) => hit.gate === 'registry-joint-branch'),
    'a match jointly probing two mutable registries before an effect branch must fire registry-joint-branch',
  )
})

test('DSL_OWNERSHIP_physical_state_record_mutable_fields_are_allowed', () => {
  const hits = scanText(readFixture('state-axes-physical.fs'), 'src/Wanxiangshu/Process/StateAxes.fs')
  assert.ok(
    hits.every((hit) => hit.gate !== 'mutable-record-field'),
    'physical-annotated records must stay green under the mutable-record-field gate',
  )
})

test('DSL_OWNERSHIP_session_mutable_requires_physical_annotation', () => {
  // Production-style multi-line record body (mirrors ChildRun / PtySession).
  const source = [
    'module Sample',
    'type Cell =',
    '    {',
    '      mutable Lease: SessionId option',
    '    }',
  ].join('\n')
  const annotated = [
    'module Sample',
    '/// DSL-state-combination: physical — lease runtime resource',
    'type Cell =',
    '    {',
    '      mutable Lease: SessionId option',
    '    }',
  ].join('\n')
  const path = 'src/Wanxiangshu/Session/Cell.fs'
  assert.ok(
    scanText(source, path).some((hit) => hit.gate === 'mutable-record-field'),
    'an unannotated Session mutable non-business field must fire mutable-record-field',
  )
  assert.ok(
    !scanText(annotated, path).some((hit) => hit.gate === 'mutable-record-field'),
    'a Session mutable non-business field behind a physical annotation must stay green',
  )
})

test('DSL_OWNERSHIP_domain_state_combination_is_explicitly_allowed', () => {
  const hits = scanText(readFixture('state-axes-domain.fs'), 'src/Wanxiangshu/Domain/StateAxes.fs')
  assert.deepEqual(hits, [])
})

test('DSL_OWNERSHIP_physical_state_combination_is_explicitly_allowed', () => {
  const hits = scanText(readFixture('state-axes-physical.fs'), 'src/Wanxiangshu/Process/StateAxes.fs')
  assert.deepEqual(hits, [])
})

test('DSL_OWNERSHIP_control_state_requires_structured_reason', () => {
  const source = [
    'module Sample',
    '/// DSL-class: ControlState',
    '/// DSL-control-state-reason: ce-equivalent=none; blockers=function-call,match!,return!,resource-scope,waiter,bounded-recursion; evidence=process-restart-reconciliation',
    'type Mode =',
    '    | A',
    '    | B',
  ].join('\n')
  assert.deepEqual(scanText(source, 'src/Wanxiangshu/Session/Sample.fs'), [])
})


test('DSL_OWNERSHIP_host_boundary_open_is_not_gate_red', () => {
  const source = ['module Sample', 'open Wanxiangshu.OpenCode', 'open Wanxiangshu.Process'].join('\n')
  assert.ok(HOST_BOUNDARY_OPEN_BASENAMES.has('HostForkRuntime.fs'))
  assert.ok(HOST_BOUNDARY_OPEN_BASENAMES.has('SatelliteRuntime.fs'))
  assert.ok(HOST_BOUNDARY_OPEN_BASENAMES.has('SyncDelegateRuntime.fs'))
  assert.ok(HOST_BOUNDARY_OPEN_BASENAMES.has('CompletionMailbox.fs'))
  assert.ok(HOST_BOUNDARY_OPEN_BASENAMES.has('ForkRuntime.fs'))
  assert.ok(HOST_BOUNDARY_OPEN_BASENAMES.has('EnforcerHost.fs'))
  assert.ok(HOST_BOUNDARY_OPEN_BASENAMES.has('HandleCompletionCodec.fs'))
  assert.ok(HOST_BOUNDARY_OPEN_BASENAMES.has('BloggerCoordinator.fs'))
  assert.ok(HOST_BOUNDARY_OPEN_BASENAMES.has('RuntimePath.fs'))
  assert.equal(isHostBoundaryOpenPath('src/Wanxiangshu/Session/HostForkRuntime.fs'), true)
  assert.equal(isHostBoundaryOpenPath('src/Wanxiangshu/Session/SatelliteRuntime.fs'), true)
  assert.equal(isHostBoundaryOpenPath('src/Wanxiangshu/Session/SyncDelegateRuntime.fs'), true)
  assert.equal(isHostBoundaryOpenPath('src/Wanxiangshu/Session/BloggerCoordinator.fs'), true)
  assert.equal(isHostBoundaryOpenPath('src/Wanxiangshu/Journal/RuntimePath.fs'), true)
  assert.deepEqual(scanText(source, 'src/Wanxiangshu/Session/HostForkRuntime.fs'), [])
  assert.deepEqual(scanText(source, 'src/Wanxiangshu/Session/BloggerCoordinator.fs'), [])
  assert.deepEqual(scanText(source, 'src/Wanxiangshu/Journal/RuntimePath.fs'), [])
  assert.ok(scanText(source, 'src/Wanxiangshu/Agent/Sample.fs').some((h) => h.gate === 'infrastructure-leak'))
})

test('DSL_OWNERSHIP_comment_only_line_is_ignored', () => {
  const source = ['module Sample', '// type HiddenCommand =', '// let mutable x = 1'].join('\n')
  assert.deepEqual(scanText(source), [])
})

test('DSL_OWNERSHIP_threshold_freeze_semantics', () => {
  assert.equal(evaluateThreshold(0, -1).ok, true)
  assert.equal(evaluateThreshold(1, -1).ok, false)
  assert.equal(evaluateThreshold(317, 317).ok, true)
  assert.equal(evaluateThreshold(318, 317).ok, false)
  assert.equal(evaluateThreshold(317, 317).reason, 'within-threshold')
  assert.equal(evaluateThreshold(318, 317).reason, 'exceeds-threshold')
})

test('DSL_OWNERSHIP_cross_file_duplicate_case_set_is_violation', () => {
  const hits = scanFiles([
    {
      file: 'src/Wanxiangshu/Domain/Alpha.fs',
      text: ['module Sample', 'type OutcomeA =', '    | Started', '    | Finished'].join('\n'),
    },
    {
      file: 'src/Wanxiangshu/Application/Beta.fs',
      text: ['module Sample', 'type OutcomeB =', '    | Finished', '    | Started'].join('\n'),
    },
  ])
  const dup = hits.filter((v) => v.gate === 'dup-cases')
  assert.ok(dup.length >= 1, 'cross-file duplicate case set must fire')
})

test('DSL_OWNERSHIP_single_file_duplicate_case_set_is_not_cross_file', () => {
  // dup-cases is a cross-file gate (PR 9 E): the global case-set map is keyed
  // across files, and each DU is flushed when the next `type` begins (or at
  // end of file). Two DUs in the SAME file, even with an identical case set,
  // are not the cross-file duplication the gate targets.
  const hits = scanFiles([
    {
      file: 'src/Wanxiangshu/Domain/Alpha.fs',
      text: ['module Sample', 'type Alpha =', '    | First', '    | Second', 'type Beta =', '    | First', '    | Second'].join('\n'),
    },
  ])
  assert.ok(!hits.some((v) => v.gate === 'dup-cases'))
})

test('DSL_OWNERSHIP_infrastructure_declared_mutable_is_accepted', () => {
  // Infrastructure is a production physical layer: a `let mutable` immediately
  // preceded by a valid `// DSL-MUTABLE: resource` declaration must be accepted
  // (declaration-based exemption, mirroring Domain/Session/Application/Process).
  const declared = [
    'module Sample',
    'let acquire () =',
    '    // DSL-MUTABLE: resource — native handle lease',
    '    let mutable handle = 0L',
    '    handle',
  ].join('\n')
  assert.deepEqual(
    scanText(declared, 'src/Wanxiangshu/Infrastructure/Resource.fs'),
    [],
    'a resource-declared mutable in Infrastructure must stay green',
  )
})

test('DSL_OWNERSHIP_journal_declared_mutable_is_accepted', () => {
  // Journal is a production persistence layer: a `let mutable` immediately
  // preceded by a valid `// DSL-MUTABLE: resource` declaration must be accepted
  // (declaration-based exemption, mirroring Infrastructure and
  // Domain/Session/Application/Process).
  const declared = [
    'module Sample',
    'let append () =',
    '    // DSL-MUTABLE: resource — index cursor lease',
    '    let mutable cursor = 0L',
    '    cursor',
  ].join('\n')
  assert.deepEqual(
    scanText(declared, 'src/Wanxiangshu/Journal/Writer.fs'),
    [],
    'a resource-declared mutable in Journal must stay green',
  )
})

test('DSL_OWNERSHIP_journal_bare_mutable_still_fires', () => {
  // Fail-closed baseline: without the declaration the same Journal mutable
  // must still be reported.
  const bare = [
    'module Sample',
    'let append () =',
    '    let mutable cursor = 0L',
    '    cursor',
  ].join('\n')
  assert.ok(
    scanText(bare, 'src/Wanxiangshu/Journal/Writer.fs').some((h) => h.gate === 'mutable'),
    'a bare mutable in Journal must fire',
  )
})

test('DSL_OWNERSHIP_infrastructure_bare_mutable_still_fires', () => {
  // Fail-closed baseline: without the declaration the same Infrastructure
  // mutable must still be reported.
  const bare = [
    'module Sample',
    'let acquire () =',
    '    let mutable handle = 0L',
    '    handle',
  ].join('\n')
  assert.ok(
    scanText(bare, 'src/Wanxiangshu/Infrastructure/Resource.fs').some((h) => h.gate === 'mutable'),
    'a bare mutable in Infrastructure must fire',
  )
})

test('DSL_OWNERSHIP_cross_file_duplicate_case_set_exemption_stays_clean', () => {
  const hits = scanFiles([
    {
      file: 'src/Wanxiangshu/Domain/PromptAuthority.fs',
      text: ['module Sample', 'type AgentNameRejection =', '    | LegacyAgentName', '    | UnknownManagedAgent', '    | Malformed'].join('\n'),
    },
    {
      file: 'src/Wanxiangshu/Infrastructure/OpenCode/Tools/ManagedAgent.fs',
      text: ['module Sample', 'type ManagedAgentParseError =', '    | UnknownManagedAgent', '    | LegacyAgentName', '    | Malformed'].join('\n'),
    },
  ])
  assert.ok(!hits.some((v) => v.gate === 'dup-cases'))
})
