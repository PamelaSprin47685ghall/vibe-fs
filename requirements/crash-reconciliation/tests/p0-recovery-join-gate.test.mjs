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

test('P0_RECOVERY_JOIN_GATE_exports_recovery_rule_ids', () => {
  for (const id of CRASH_RULES) {
    assert.ok(RULE_IDS.includes(id), `missing recovery rule id: ${id}`)
  }
  // Checker integrity invariant (shared oracle, kept here).
  assert.equal(RULES.length, RULE_IDS.length)
})

for (const sample of NEGATIVES) {
  const variant = sample.variant === undefined ? '' : `_${sample.variant}`
  test(`P0_RECOVERY_JOIN_GATE_negative_${sample.id}${variant}_goes_red`, () => {
    const hits = scanText(sample.source, sample.file)
    const ofId = hits.filter((h) => h.id === sample.id)
    assert.ok(ofId.length >= 1, `expected rule ${sample.id} to fire; got ${hits.map((h) => h.id).join(',')}`)
  })
}

test('P0_RECOVERY_JOIN_GATE_join_tool_missing_recovery_goes_red', () => {
  const source = [
    'module JoinTool',
    'let execute scope context =',
    '    let! joined = Join.joinAvailable runtime permit MaxJoinBatch interrupt.Wait',
  ].join('\n')
  const hits = scanText(source, 'JoinTool.fs')
  assert.ok(hits.some((h) => h.id === 'join-tool-family-recovery'))
  assert.ok(hits.some((h) => h.id === 'join-tool-family-blocked'))
})

test('P0_RECOVERY_JOIN_GATE_join_tool_with_dsl_stays_green_for_positive', () => {
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

test('P0_RECOVERY_JOIN_GATE_join_tool_bare_runtime_join_goes_red', () => {
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

test('P0_RECOVERY_JOIN_GATE_host_fork_restart_missing_proof_goes_red', () => {
  const hits = scanText(HOST_FORK_RESTART_MISSING_PROOF, 'HostForkRestart.fs')
  assert.ok(
    hits.some((h) => h.id === 'host-fork-restart-proof-structure'),
    `expected host-fork-restart-proof-structure; got ${hits.map((h) => h.id).join(',')}`,
  )
})

test('P0_RECOVERY_JOIN_GATE_host_fork_restart_with_terminal_structure_stays_green', () => {
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

test('P0_RECOVERY_JOIN_GATE_bare_join_allowlist_host_fork_stays_green', () => {
  const source = [
    'module HostForkRuntime',
    'let raceChangeAndMailbox durable fromRev ms =',
    '    let! joined = runtime.Join(timeoutMs = ms)',
    '    return Choice2Of2 joined',
  ].join('\n')
  const hits = scanText(source, 'HostForkRuntime.fs')
  assert.ok(!hits.some((h) => h.id === 'tools-no-bare-runtime-join'))
})

test('P0_RECOVERY_JOIN_GATE_executor_permit_path_stays_green', () => {
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
    '    member _.JoinWithPermit(timeoutMs) =',
    '        match! requirePermit () with',
    '        | Ok permit -> runtime.JoinWithPermit(permit)',
  ].join('\n')
  const host = [
    'module HostForkRuntime',
    'member this.JoinWithPermit(permit) =',
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

test('P0_RECOVERY_JOIN_GATE_production_sources_are_green', () => {
  const files = [
    'src/Wanxiangshu/Session/HostForkRunLifecycle.fs',
    'src/Wanxiangshu/Session/ForkRecovery.fs',
    'src/Wanxiangshu/Session/HostForkRestart.fs',
    'src/Wanxiangshu/Session/ForkRuntime.fs',
    'src/Wanxiangshu/Session/HostForkRuntime.fs',
    'src/Wanxiangshu/Session/HostForkAgent.fs',
    'src/Wanxiangshu/Session/HandleController.fs',
    'src/Wanxiangshu/Session/AgentCompletion.fs',
    'src/Wanxiangshu/Session/HandleCompletionCodec.fs',
    'src/Wanxiangshu/Session/CompletionMailbox.fs',
    'src/Wanxiangshu/Session/JoinDrain.fs',
    'src/Wanxiangshu/Domain/ChildRecovery.fs',
    'src/Wanxiangshu/Application/Reconciliation/Join.fs',
    'src/Wanxiangshu/Domain/SessionRecovery.fs',
    'src/Wanxiangshu/Kernel/Fact.fs',
    'src/Wanxiangshu/Application/Reconciliation/ChildRecoveryWorkflow.fs',
    'src/Wanxiangshu/Application/Reconciliation/SessionRecoveryWorkflow.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Host/PluginRuntimeScope.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Plugin/SpikePlugin.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/JoinTool.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/ExecutorTool.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/Distillation.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/DistillationRuntime.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Codec/JoinResultRenderer.fs',
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

test('P0_RECOVERY_JOIN_GATE_positive_recovery_shapes_present', () => {
  const child = readFileSync(join(ROOT, 'src/Wanxiangshu/Domain/ChildRecovery.fs'), 'utf8')
  const mailbox = readFileSync(join(ROOT, 'src/Wanxiangshu/Session/CompletionMailbox.fs'), 'utf8')
  const ports = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Application/Reconciliation/SessionRecoveryWorkflow.fs'),
    'utf8',
  )
  const joinOps = readFileSync(join(ROOT, 'src/Wanxiangshu/Application/Reconciliation/Join.fs'), 'utf8')

  for (const [file, text, ids] of [
    ['ChildRecovery.fs', child, ['joinable-from-decoded', 'child-recovery-result-five-cases']],
    ['CompletionMailbox.fs', mailbox, ['mailbox-pulse-agent-handle', 'mailbox-publish-pty-completion']],
    [
      'SessionRecoveryWorkflow.fs',
      ports,
      ['session-ports-restore-handles-mandatory', 'session-ports-recover-jobs-mandatory'],
    ],
    ['Join.fs', joinOps, ['join-program-requires-permit']],
  ]) {
    const hits = scanText(text, file)
    for (const id of ids) {
      assert.ok(
        !hits.some((h) => h.id === id),
        `positive rule ${id} must be satisfied in ${file}; hits=${hits.filter((h) => h.id === id).map((h) => h.text).join('|')}`,
      )
    }
  }
})
