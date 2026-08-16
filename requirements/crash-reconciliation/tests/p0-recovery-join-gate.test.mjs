// Split from tests/unit/verify/p0-recovery-join-gate.test.mjs (cutover Wave 2a); owner: crash-reconciliation
//
// P0-RECOVERY-JOIN-001 §10 静态 gate — recovery 规则侧（CRASH-001..012/CRASH-009/012）：
// join 必须走 family-recovery permit、recordCompletion 单一 owner 协议、session ports
// 强制、child-recovery 五结果、host-fork-restart proof 结构等。aborted≠terminal 规则
// 归 effect-accounting（p0-recovery-join-clean-break.test.mjs）；LOOP-006 桥接静态形状
// （lifecycle-aborted-record / record-completion-single-owner）归 degeneration-guard。
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import {
  RULE_IDS,
  RULES,
  scanFiles,
  scanText,
} from '../../../scripts/checks/p0-recovery-join.mjs'

const ROOT = new URL('../../../', import.meta.url).pathname

/** recovery 侧规则（crash-reconciliation owner）。 */
const CRASH_RULES = new Set([
  'join-tool-family-recovery',
  'join-tool-family-blocked',
  'join-tool-no-bare-runtime-join',
  'join-tool-join-program',
  'tools-no-bare-runtime-join',
  'executor-tool-require-permit',
  'executor-tool-empty-session-fail-closed',
  'distillation-join-with-permit',
  'distillation-runtime-join-with-permit',
  'join-with-permit-closure-digest',
  'restore-handles-none-no-recovery',
  'recover-job-none-no-recovery',
  'spike-restore-handles-none',
  'host-fork-runtime-recovery-task',
  'host-fork-runtime-await-recovery-call',
  'awaiting-evidence-case',
  'joinable-from-decoded',
  'session-ports-restore-handles-mandatory',
  'session-ports-recover-jobs-mandatory',
  'child-recovery-result-five-cases',
  'join-program-requires-permit',
  'mailbox-pulse-agent-handle',
  'mailbox-publish-pty-completion',
  'lifecycle-aborted-setresult',
  'fork-recovery-synthetic-restored',
  'fork-recovery-interrupted-finality',
  'ensure-recovery-unit',
  'missing-ports-family-ready',
  'host-fork-restart-proof-structure',
])

const NEGATIVES = [
  {
    id: 'awaiting-evidence-case',
    file: 'ChildRecovery.fs',
    source: [
      'type ChildRecoveryResult =',
      '    | RecoveredActive of x',
      '    | AwaitingEvidence of reason: string',
    ].join('\n'),
  },
  {
    id: 'lifecycle-aborted-setresult',
    file: 'HostForkRunLifecycle.fs',
    source: [
      'module HostForkRunLifecycle',
      'match outcome with',
      '| Aborted reason ->',
      '    run.Source.SetResult (Ok abortedOutcome)',
    ].join('\n'),
  },
  {
    id: 'fork-recovery-synthetic-restored',
    file: 'ForkRecovery.fs',
    source: [
      'module ForkRecovery',
      'let restore x =',
      '    AgentCompletion.ofSimpleText id runId role "(restored from journal)"',
    ].join('\n'),
  },
  {
    id: 'fork-recovery-synthetic-restored',
    variant: 'paren-form',
    file: 'ForkRecovery.fs',
    source: [
      'module ForkRecovery',
      'let restore x =',
      '    AgentCompletion.ofSimpleText(id, runId, role, "(restored from journal)")',
    ].join('\n'),
  },
  {
    id: 'fork-recovery-interrupted-finality',
    file: 'ForkRecovery.fs',
    source: [
      'module ForkRecovery',
      'let markInterrupted agentId reason agents =',
      '    let c = ChildRun.makeAborted run reason',
      '    agents',
    ].join('\n'),
  },
  {
    id: 'ensure-recovery-unit',
    file: 'PluginRuntimeScope.fs',
    source: [
      'type PluginRuntimeScope() =',
      '    member this.EnsureRecoveryDone(root: SessionId) : Task<unit> =',
      '        task { return () }',
    ].join('\n'),
  },
  {
    id: 'missing-ports-family-ready',
    file: 'PluginRuntimeScope.fs',
    source: [
      'member this.RequireFamilyRecovery(root) =',
      '    match familyRecoveryPorts with',
      '    | None -> FamilyRecovery.FamilyReady fakePermit',
      '    | Some ports -> recover ports root',
    ].join('\n'),
  },
  {
    id: 'restore-handles-none-no-recovery',
    file: 'SessionRecoveryWorkflow.fs',
    source: [
      'match ports.RestoreHandles with',
      '| Some restore -> restore sessionId',
      '| None -> Task.FromResult(SessionRecovery.NoRecoveryRequired(emptyReceipt sessionId sequence))',
    ].join('\n'),
  },
  {
    id: 'recover-job-none-no-recovery',
    file: 'SessionRecoveryWorkflow.fs',
    source: [
      'match ports.RecoverJob with',
      '| Some recover -> recover jobId',
      '| None -> Task.FromResult(SessionRecovery.NoRecoveryRequired(emptyReceipt sessionId sequence))',
    ].join('\n'),
  },
  {
    id: 'spike-restore-handles-none',
    file: 'SpikePlugin.fs',
    source: [
      'scope.AttachFamilyRecoveryPorts(',
      '    { Journal = journal',
      '      RestoreHandles = None',
      '      RecoverJob = None })',
    ].join('\n'),
  },
  {
    id: 'host-fork-runtime-recovery-task',
    file: 'HostForkRuntime.fs',
    source: [
      'let mutable recoveryTask: Task option = None',
      'member private _.EnsureChildRestoreStarted() =',
      '    recoveryTask <- Some t',
    ].join('\n'),
  },
  {
    id: 'host-fork-runtime-await-recovery-call',
    file: 'HostForkAgent.fs',
    source: [
      'task {',
      '    do! this.AwaitRecovery()',
      '    let retired = this.IsRetiredHandle agentId',
    ].join('\n'),
  },
  {
    id: 'join-tool-no-bare-runtime-join',
    file: 'JoinTool.fs',
    source: [
      'module JoinTool',
      'let execute scope context =',
      '    let! recovery = scope.RequireFamilyRecovery root',
      '    match recovery with',
      '    | FamilyReady permit ->',
      '        match! runtime.Join() with',
      '        | Ok c -> encode c',
      '    | FamilyBlocked b -> recoveryBlocked b',
    ].join('\n'),
  },
  {
    id: 'tools-no-bare-runtime-join',
    file: 'Distillation.fs',
    source: [
      'module Distillation',
      'let awaitAgent runtime agentId stash =',
      '    let! joined = runtime.Join(Some remainingMs)',
    ].join('\n'),
  },
  {
    id: 'tools-no-bare-runtime-join',
    variant: 'executor-tool',
    file: 'ExecutorTool.fs',
    source: [
      'module ExecutorTool',
      'let execute scope context =',
      '    match! runtime.Join() with',
      '    | Ok c -> encode c',
    ].join('\n'),
  },
  {
    id: 'tools-no-bare-runtime-join',
    variant: 'distillation-runtime',
    file: 'DistillationRuntime.fs',
    source: [
      'module DistillationRuntime',
      'member _.Join(timeoutMs) =',
      '    match timeoutMs with',
      '    | Some ms -> runtime.Join(timeoutMs = ms)',
      '    | None -> runtime.Join()',
    ].join('\n'),
  },
  {
    id: 'executor-tool-empty-session-fail-closed',
    file: 'ExecutorTool.fs',
    source: [
      'module ExecutorTool',
      'if String.IsNullOrWhiteSpace context.SessionId then',
      '    return true',
      'else',
      '    let! recovery = scope.RequireFamilyRecovery root',
    ].join('\n'),
  },
]

const HOST_FORK_RESTART_MISSING_PROOF = [
  'module HostForkRestart',
  'let recoverChild runtime agentId childSessionId =',
  '    runtime.Restore(agentId)',
  '    runtime.MarkInterrupted(agentId, "no proof path")',
].join('\n')

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_GATE_exports_recovery_rule_ids', () => {
  for (const id of CRASH_RULES) {
    assert.ok(RULE_IDS.includes(id), `missing recovery rule id: ${id}`)
  }
  // Checker integrity invariant (shared oracle, kept here).
  assert.equal(RULES.length, RULE_IDS.length)
})

// 一 test 一 WHAT：NEGATIVES 按命题归属拆分，checker 只认静态标题里的 WHAT[<ID>]。
const assertNegative = (sample) => {
  const hits = scanText(sample.source, sample.file)
  const ofId = hits.filter((h) => h.id === sample.id)
  assert.ok(ofId.length >= 1, `expected rule ${sample.id} to fire; got ${hits.map((h) => h.id).join(',')}`)
}
const neg = (id, variant) => NEGATIVES.find((s) => s.id === id && (s.variant ?? '') === (variant ?? ''))

// CRASH-010：恢复结果分支穷尽（AwaitingEvidence 被 RecoveryIncomplete|RecoveryBlocked 取代）。
test('WHAT[CRASH-010] P0_RECOVERY_JOIN_GATE_negative_awaiting-evidence-case_goes_red', () => assertNegative(neg('awaiting-evidence-case')))
test('WHAT[CRASH-010] P0_RECOVERY_JOIN_GATE_negative_restore-handles-none-no-recovery_goes_red', () => assertNegative(neg('restore-handles-none-no-recovery')))
test('WHAT[CRASH-010] P0_RECOVERY_JOIN_GATE_negative_recover-job-none-no-recovery_goes_red', () => assertNegative(neg('recover-job-none-no-recovery')))

// CRASH-009：child recovery 无 Aborted 终态；JoinableCompletion 禁止合成/任意 body 证明。
test('WHAT[CRASH-009] P0_RECOVERY_JOIN_GATE_negative_lifecycle-aborted-setresult_goes_red', () => assertNegative(neg('lifecycle-aborted-setresult')))
test('WHAT[CRASH-009] P0_RECOVERY_JOIN_GATE_negative_fork-recovery-synthetic-restored_goes_red', () => assertNegative(neg('fork-recovery-synthetic-restored')))
test('WHAT[CRASH-009] P0_RECOVERY_JOIN_GATE_negative_fork-recovery-synthetic-restored_paren-form_goes_red', () => assertNegative(neg('fork-recovery-synthetic-restored', 'paren-form')))
test('WHAT[CRASH-009] P0_RECOVERY_JOIN_GATE_negative_fork-recovery-interrupted-finality_goes_red', () => assertNegative(neg('fork-recovery-interrupted-finality')))

// CRASH-004：恢复不发明程序计数器 / 第二状态机。
test('WHAT[CRASH-004] P0_RECOVERY_JOIN_GATE_negative_ensure-recovery-unit_goes_red', () => assertNegative(neg('ensure-recovery-unit')))
test('WHAT[CRASH-004] P0_RECOVERY_JOIN_GATE_negative_host-fork-runtime-recovery-task_goes_red', () => assertNegative(neg('host-fork-runtime-recovery-task')))
test('WHAT[CRASH-004] P0_RECOVERY_JOIN_GATE_negative_host-fork-runtime-await-recovery-call_goes_red', () => assertNegative(neg('host-fork-runtime-await-recovery-call')))

// CRASH-006：没有 fresh evidence 就没有自动 effect；permit 才可 join。
test('WHAT[CRASH-006] P0_RECOVERY_JOIN_GATE_negative_missing-ports-family-ready_goes_red', () => assertNegative(neg('missing-ports-family-ready')))
test('WHAT[CRASH-006] P0_RECOVERY_JOIN_GATE_negative_join-tool-no-bare-runtime-join_goes_red', () => assertNegative(neg('join-tool-no-bare-runtime-join')))
test('WHAT[CRASH-006] P0_RECOVERY_JOIN_GATE_negative_tools-no-bare-runtime-join_goes_red', () => assertNegative(neg('tools-no-bare-runtime-join')))
test('WHAT[CRASH-006] P0_RECOVERY_JOIN_GATE_negative_tools-no-bare-runtime-join_executor-tool_goes_red', () => assertNegative(neg('tools-no-bare-runtime-join', 'executor-tool')))
test('WHAT[CRASH-006] P0_RECOVERY_JOIN_GATE_negative_tools-no-bare-runtime-join_distillation-runtime_goes_red', () => assertNegative(neg('tools-no-bare-runtime-join', 'distillation-runtime')))

// CRASH-005：证据不足 / 端口缺失 fail closed。
test('WHAT[CRASH-005] P0_RECOVERY_JOIN_GATE_negative_executor-tool-empty-session-fail-closed_goes_red', () => assertNegative(neg('executor-tool-empty-session-fail-closed')))

// CRASH-017：plugin load 不是 recovery trigger，普通生命周期不接线。
test('WHAT[CRASH-017] P0_RECOVERY_JOIN_GATE_negative_spike-restore-handles-none_goes_red', () => assertNegative(neg('spike-restore-handles-none')))

test('WHAT[CRASH-006] P0_RECOVERY_JOIN_GATE_join_tool_missing_recovery_goes_red', () => {
  const source = [
    'module JoinTool',
    'let execute scope context =',
    '    let! joined = Join.joinAvailable runtime permit MaxJoinBatch interrupt.Wait',
  ].join('\n')
  const hits = scanText(source, 'JoinTool.fs')
  assert.ok(hits.some((h) => h.id === 'join-tool-family-recovery'))
  assert.ok(hits.some((h) => h.id === 'join-tool-family-blocked'))
})

test('WHAT[CRASH-006] P0_RECOVERY_JOIN_GATE_join_tool_with_dsl_stays_green_for_positive', () => {
  // EXEC-018 / PR5 production shape: direct Join.joinAvailable (no AST).
  const source = [
    'module JoinTool',
    'let execute scope context =',
    '    let! recovery = scope.RequireFamilyRecovery root',
    '    match recovery with',
    '    | FamilyReady permit ->',
    '        let! joined = Join.joinAvailable runtime permit MaxJoinBatch interrupt.Wait',
    '        match joined with',
    '        | Ok c -> encode c',
    '    | FamilyBlocked b -> recoveryBlocked b',
  ].join('\n')
  const hits = scanText(source, 'JoinTool.fs')
  assert.ok(!hits.some((h) => h.id === 'join-tool-family-recovery'))
  assert.ok(!hits.some((h) => h.id === 'join-tool-family-blocked'))
  assert.ok(!hits.some((h) => h.id === 'join-tool-join-program'))
  assert.ok(!hits.some((h) => h.id === 'join-tool-no-bare-runtime-join'))
})

test('WHAT[CRASH-011] P0_RECOVERY_JOIN_GATE_join_tool_bare_runtime_join_goes_red', () => {
  const source = [
    'module JoinTool',
    'let execute scope context =',
    '    let! recovery = scope.RequireFamilyRecovery root',
    '    match recovery with',
    '    | FamilyReady _ -> match! runtime.Join() with',
    '    | FamilyBlocked b -> recoveryBlocked b',
  ].join('\n')
  const hits = scanText(source, 'JoinTool.fs')
  assert.ok(hits.some((h) => h.id === 'join-tool-no-bare-runtime-join'))
  assert.ok(hits.some((h) => h.id === 'join-tool-join-program'))
})

test('WHAT[CRASH-012] P0_RECOVERY_JOIN_GATE_host_fork_restart_missing_proof_goes_red', () => {
  const hits = scanText(HOST_FORK_RESTART_MISSING_PROOF, 'HostForkRestart.fs')
  assert.ok(
    hits.some((h) => h.id === 'host-fork-restart-proof-structure'),
    `expected host-fork-restart-proof-structure; got ${hits.map((h) => h.id).join(',')}`,
  )
})

test('WHAT[CRASH-012] P0_RECOVERY_JOIN_GATE_host_fork_restart_with_terminal_structure_stays_green', () => {
  // EXEC-021/024: only fromDecoded + PulseAgentHandle; no tryFromDurableCompleted / PublishCompletion.
  const source = [
    'module HostForkRestart',
    'match! ChildRecoveryWorkflow.resolveAndCommit ports with',
    '| Ok (Joinable proof) -> ()',
    'match HandleCompletionCodec.decodeBody body with',
    '| Current decoded ->',
    '    let proof = JoinableCompletion.fromDecoded agentId handle child decoded body',
    '    do HandleController.recordCompletion journal parentId proof',
    '    runtime.PulseAgentHandle agentHandle',
    '| LegacyFalseAbort _ -> ()',
  ].join('\n')
  const hits = scanText(source, 'HostForkRestart.fs')
  assert.ok(!hits.some((h) => h.id === 'host-fork-restart-proof-structure'))
  assert.ok(!hits.some((h) => h.id === 'host-fork-restart-false-finality'))
  assert.ok(!hits.some((h) => h.id === 'host-fork-restart-bare-publish'))
  assert.ok(!hits.some((h) => h.id === 'try-from-durable-completed'))
  assert.ok(!hits.some((h) => h.id === 'publish-completion-agent'))
})

test('WHAT[CRASH-011] P0_RECOVERY_JOIN_GATE_bare_join_allowlist_host_fork_stays_green', () => {
  const source = [
    'module HostForkRuntime',
    'let raceChangeAndMailbox durable fromRev ms =',
    '    let! joined = runtime.Join(timeoutMs = ms)',
    '    return Choice2Of2 joined',
  ].join('\n')
  const hits = scanText(source, 'HostForkRuntime.fs')
  assert.ok(!hits.some((h) => h.id === 'tools-no-bare-runtime-join'))
})

test('WHAT[CRASH-011] P0_RECOVERY_JOIN_GATE_executor_permit_path_stays_green', () => {
  const tool = [
    'module ExecutorTool',
    'if String.IsNullOrWhiteSpace context.SessionId then',
    '    return error "Missing sessionID"',
    'else',
    '    let requirePermit () =',
    '        task {',
    '            let! recovery = scope.RequireFamilyRecovery root',
    '            match recovery with',
    '            | FamilyReady permit -> return Ok permit',
    '        }',
    '    let! summary =',
    '        Distillation.distillSpool',
    '            (Distillation.asDistillationRuntime runtime requirePermit)',
    '            spoolPath',
  ].join('\n')
  const summarize = [
    'module Distillation',
    'let awaitAgentWithPermit runtime agentId =',
    '    let! joined = runtime.AwaitAgentWithPermit(agentId, Some AwaitAgentTimeoutMs)',
  ].join('\n')
  const wrap = [
    'module DistillationRuntime',
    'let asDistillationRuntime runtime requirePermit =',
    '    member _.AwaitAgentWithPermit(agentId, timeoutMs) =',
    '        match! requirePermit () with',
    '        | Ok permit -> runtime.AwaitAgentWithPermit(agentId, timeoutMs)',
  ].join('\n')
  const host = [
    'module HostForkRuntime',
    'member this.AwaitAgentWithPermit(agentId, timeoutMs) =',
    '    let permitDigest = FamilyRecoveryPermit.closureDigest permit',
    '    let current = RecoveryClosureProjection.discover root projections currentSeq',
    '    if current.Digest <> permitDigest then Error mismatch',
  ].join('\n')
  assert.ok(!scanText(tool, 'ExecutorTool.fs').some((h) => h.id === 'executor-tool-empty-session-fail-closed'))
  assert.ok(!scanText(tool, 'ExecutorTool.fs').some((h) => h.id === 'executor-tool-require-permit'))
  assert.ok(!scanText(tool, 'ExecutorTool.fs').some((h) => h.id === 'tools-no-bare-runtime-join'))
  assert.ok(!scanText(summarize, 'Distillation.fs').some((h) => h.id === 'distillation-join-with-permit'))
  assert.ok(!scanText(summarize, 'Distillation.fs').some((h) => h.id === 'tools-no-bare-runtime-join'))
  assert.ok(!scanText(wrap, 'DistillationRuntime.fs').some((h) => h.id === 'distillation-runtime-join-with-permit'))
  assert.ok(!scanText(wrap, 'DistillationRuntime.fs').some((h) => h.id === 'tools-no-bare-runtime-join'))
  assert.ok(!scanText(host, 'HostForkRuntime.fs').some((h) => h.id === 'join-with-permit-closure-digest'))
})

test('WHAT[CRASH-012] P0_RECOVERY_JOIN_GATE_production_sources_are_green', () => {
  const files = [
    'src/Wanxiangshu/Execution/Delegation/Fork/Host/RunLifecycle.fs',
    'src/Wanxiangshu/Execution/Delegation/Fork/Recovery.fs',
    'src/Wanxiangshu/Execution/Delegation/Fork/Host/Restart.fs',
    'src/Wanxiangshu/Execution/Delegation/Fork/Runtime.fs',
    'src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs',
    'src/Wanxiangshu/Execution/Delegation/Fork/Host/Agent.fs',
    'src/Wanxiangshu/Execution/Delegation/Handle/Controller.fs',
    'src/Wanxiangshu/Execution/Session/AgentCompletion.fs',
    'src/Wanxiangshu/Execution/Delegation/Handle/CompletionCodec.fs',
    'src/Wanxiangshu/Execution/Session/Wait/CompletionMailbox.fs',
    'src/Wanxiangshu/Execution/Delegation/Handle/JoinDrain.fs',
    'src/Wanxiangshu/Execution/Delegation/Fork/ChildRecovery.fs',
    'src/Wanxiangshu/Execution/Delegation/Join.fs',
    'src/Wanxiangshu/Execution/Session/Recovery/Model.fs',
    'src/Wanxiangshu/Execution/Delegation/Facts.fs',
    'src/Wanxiangshu/Execution/Delegation/ChildRecoveryWorkflow.fs',
    'src/Wanxiangshu/Execution/Session/Recovery/Workflow.fs',
    'src/Wanxiangshu/OpenCode/Host/PluginRuntimeScope.fs',
    'src/Wanxiangshu/OpenCode/Plugin/SpikePlugin.fs',
    'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/JoinTool.fs',
    'src/Wanxiangshu/OpenCode/Tools/ExecutorTool.fs',
    'src/Wanxiangshu/OpenCode/Tools/Distillation.fs',
    'src/Wanxiangshu/OpenCode/Tools/DistillationRuntime.fs',
    'src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/JoinResultRenderer.fs',
  ]
  const entries = files.map((rel) => ({
    file: rel,
    text: readFileSync(join(ROOT, rel), 'utf8'),
  }))
  const hits = scanFiles(entries).filter((h) => CRASH_RULES.has(h.id))
  assert.deepEqual(
    hits,
    [],
    hits.map((h) => `${h.id}@${h.file}:${h.line}`).join('; '),
  )
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_GATE_positive_child_recovery_shapes_present', () => {
  const child = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Fork/ChildRecovery.fs'), 'utf8')
  for (const id of ['joinable-from-decoded', 'child-recovery-result-five-cases']) {
    const hits = scanText(child, 'ChildRecovery.fs')
    assert.ok(
      !hits.some((h) => h.id === id),
      `positive rule ${id} must be satisfied in ChildRecovery.fs; hits=${hits.filter((h) => h.id === id).map((h) => h.text).join('|')}`,
    )
  }
})

test('WHAT[CRASH-012] P0_RECOVERY_JOIN_GATE_positive_mailbox_pulse_shape_present', () => {
  const mailbox = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Wait/CompletionMailbox.fs'), 'utf8')
  for (const id of ['mailbox-pulse-agent-handle', 'mailbox-publish-pty-completion']) {
    const hits = scanText(mailbox, 'CompletionMailbox.fs')
    assert.ok(
      !hits.some((h) => h.id === id),
      `positive rule ${id} must be satisfied in CompletionMailbox.fs; hits=${hits.filter((h) => h.id === id).map((h) => h.text).join('|')}`,
    )
  }
})

test('WHAT[CRASH-002] P0_RECOVERY_JOIN_GATE_positive_session_ports_shapes_present', () => {
  const ports = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Workflow.fs'),
    'utf8',
  )
  for (const id of ['session-ports-restore-handles-mandatory', 'session-ports-recover-jobs-mandatory']) {
    const hits = scanText(ports, 'SessionRecoveryWorkflow.fs')
    assert.ok(
      !hits.some((h) => h.id === id),
      `positive rule ${id} must be satisfied in SessionRecoveryWorkflow.fs; hits=${hits.filter((h) => h.id === id).map((h) => h.text).join('|')}`,
    )
  }
})

test('WHAT[CRASH-011] P0_RECOVERY_JOIN_GATE_positive_join_program_requires_permit_shape_present', () => {
  const joinOps = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Join.fs'), 'utf8')
  const hits = scanText(joinOps, 'Join.fs')
  assert.ok(
    !hits.some((h) => h.id === 'join-program-requires-permit'),
    `positive rule join-program-requires-permit must be satisfied in Join.fs; hits=${hits.filter((h) => h.id === 'join-program-requires-permit').map((h) => h.text).join('|')}`,
  )
})
