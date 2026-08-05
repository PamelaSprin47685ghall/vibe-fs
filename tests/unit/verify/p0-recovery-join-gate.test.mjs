/**
 * P0-RECOVERY-JOIN-001 §10: architecture gate negatives (synthetic) + production green.
 */
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

const NEGATIVES = [
  {
    id: 'agent-aborted-type',
    file: 'AgentCompletion.fs',
    source: [
      'type AgentCompletionOutcome =',
      '    | AgentCompleted of payload',
      '    | AgentAborted of reason: string',
    ].join('\n'),
  },
  {
    id: 'agent-completion-aborted-factory',
    file: 'AgentCompletion.fs',
    source: [
      'module AgentCompletion',
      'let bad a r = AgentCompletion.aborted a r None None "X" "y"',
    ].join('\n'),
  },
  {
    id: 'child-run-make-aborted',
    file: 'ChildRun.fs',
    source: ['module ChildRun', 'let c = ChildRun.makeAborted run reason'].join('\n'),
  },
  {
    id: 'aborted-run-factory',
    file: 'domain.mjs',
    source: ['export const abortedRun = (id) => ({ status: "aborted", id })'].join('\n'),
  },
  {
    id: 'try-from-durable-completed',
    file: 'ChildRecovery.fs',
    source: [
      'match JoinableCompletion.tryFromDurableCompleted agentId handle child kind body with',
      '| Ok c -> Ok c',
    ].join('\n'),
  },
  {
    id: 'publish-completion-agent',
    file: 'ForkRuntime.fs',
    source: [
      'member _.PublishCompletion(c: RunCompletion) =',
      '    mailbox.Publish c',
    ].join('\n'),
  },
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
    id: 'lifecycle-aborted-completion',
    file: 'HostForkRunLifecycle.fs',
    source: [
      'module HostForkRunLifecycle',
      'let bad run =',
      '    AgentCompletion.aborted run.AgentId run.RunId None None "X" "y"',
    ].join('\n'),
  },
  {
    id: 'lifecycle-aborted-record',
    file: 'HostForkRunLifecycle.fs',
    source: [
      'module HostForkRunLifecycle',
      'match outcome with',
      '| Aborted reason ->',
      '    HandleController.recordCompletion journal parentId proof',
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
    file: 'SessionRecoveryInterpreter.fs',
    source: [
      'match ports.RestoreHandles with',
      '| Some restore -> restore sessionId',
      '| None -> Task.FromResult(SessionRecovery.NoRecoveryRequired(emptyReceipt sessionId sequence))',
    ].join('\n'),
  },
  {
    id: 'recover-job-none-no-recovery',
    file: 'SessionRecoveryInterpreter.fs',
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
    id: 'host-fork-restart-false-finality',
    file: 'HostForkRestart.fs',
    source: [
      'module HostForkRestart',
      'let recoverChild runtime agentId =',
      '    let c = AgentCompletion.aborted agentId runId None None "X" "restart abort"',
      '    runtime.PublishCompletion c',
    ].join('\n'),
  },
  {
    id: 'host-fork-restart-bare-publish',
    file: 'HostForkRestart.fs',
    source: [
      'module HostForkRestart',
      'let recoverChild runtime =',
      '    let payload = AgentCompletion.completed agentId childSessionId runId role root run text None',
      '    runtime.PublishCompletion { Outcome = payload }',
    ].join('\n'),
  },
  {
    id: 'fork-runtime-parent-cancelled-aborted',
    file: 'ForkRuntime.fs',
    source: [
      'match error with',
      '| AgentError.ParentCancelled -> ChildRun.makeAborted childRun "parent cancelled"',
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
    file: 'ExecutorSummarize.fs',
    source: [
      'module ExecutorSummarize',
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
    variant: 'executor-summarize-runtime',
    file: 'ExecutorSummarizeRuntime.fs',
    source: [
      'module ExecutorSummarizeRuntime',
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
  {
    id: 'record-completion-single-owner',
    file: 'HostForkRunLifecycle.fs',
    source: [
      'module HostForkRunLifecycle',
      'let deliver journal parentId proof =',
      '    HandleController.recordCompletion journal parentId proof',
    ].join('\n'),
  },
]

const HOST_FORK_RESTART_MISSING_PROOF = [
  'module HostForkRestart',
  'let recoverChild runtime agentId childSessionId =',
  '    runtime.Restore(agentId)',
  '    runtime.MarkInterrupted(agentId, "no proof path")',
].join('\n')

test('P0_RECOVERY_JOIN_GATE_exports_rule_ids', () => {
  assert.ok(RULE_IDS.includes('join-tool-family-recovery'))
  assert.ok(RULE_IDS.includes('join-tool-no-bare-runtime-join'))
  assert.ok(RULE_IDS.includes('join-tool-join-program'))
  assert.ok(RULE_IDS.includes('tools-no-bare-runtime-join'))
  assert.ok(RULE_IDS.includes('executor-tool-require-permit'))
  assert.ok(RULE_IDS.includes('executor-summarize-join-with-permit'))
  assert.ok(RULE_IDS.includes('executor-runtime-join-with-permit'))
  assert.ok(RULE_IDS.includes('join-with-permit-closure-digest'))
  assert.ok(RULE_IDS.includes('lifecycle-aborted-completion'))
  assert.ok(RULE_IDS.includes('record-completion-single-owner'))
  assert.ok(RULE_IDS.includes('restore-handles-none-no-recovery'))
  assert.ok(RULE_IDS.includes('recover-job-none-no-recovery'))
  assert.ok(RULE_IDS.includes('spike-restore-handles-none'))
  assert.ok(RULE_IDS.includes('host-fork-runtime-recovery-task'))
  assert.ok(RULE_IDS.includes('host-fork-runtime-await-recovery-call'))
  // EXEC-020..024 clean-break rules
  assert.ok(RULE_IDS.includes('agent-aborted-type'))
  assert.ok(RULE_IDS.includes('agent-completion-aborted-factory'))
  assert.ok(RULE_IDS.includes('child-run-make-aborted'))
  assert.ok(RULE_IDS.includes('aborted-run-factory'))
  assert.ok(RULE_IDS.includes('try-from-durable-completed'))
  assert.ok(RULE_IDS.includes('publish-completion-agent'))
  assert.ok(RULE_IDS.includes('awaiting-evidence-case'))
  assert.ok(RULE_IDS.includes('agent-outcome-completed-case'))
  assert.ok(RULE_IDS.includes('agent-outcome-failed-case'))
  assert.ok(RULE_IDS.includes('agent-outcome-abandoned-case'))
  assert.ok(RULE_IDS.includes('agent-join-item-three-cases'))
  assert.ok(RULE_IDS.includes('pty-aborted-retained'))
  assert.ok(RULE_IDS.includes('completion-blob-schema-v2'))
  assert.ok(RULE_IDS.includes('legacy-false-abort-decode'))
  assert.ok(RULE_IDS.includes('joinable-from-decoded'))
  assert.ok(RULE_IDS.includes('session-ports-restore-handles-mandatory'))
  assert.ok(RULE_IDS.includes('session-ports-recover-jobs-mandatory'))
  assert.ok(RULE_IDS.includes('child-recovery-result-five-cases'))
  assert.ok(RULE_IDS.includes('join-program-requires-permit'))
  assert.ok(RULE_IDS.includes('mailbox-pulse-agent-handle'))
  assert.ok(RULE_IDS.includes('mailbox-publish-pty-completion'))
  assert.ok(RULE_IDS.includes('false-completion-rejected-fact'))
  assert.ok(RULE_IDS.includes('parent-join-correction-fact'))
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
    '    let program = JoinProgram.joinAny permit',
    '    let! joined = JoinInterpreter.interpret runtime program',
  ].join('\n')
  const hits = scanText(source, 'JoinTool.fs')
  assert.ok(hits.some((h) => h.id === 'join-tool-family-recovery'))
  assert.ok(hits.some((h) => h.id === 'join-tool-family-blocked'))
})

test('P0_RECOVERY_JOIN_GATE_join_tool_with_dsl_stays_green_for_positive', () => {
  // EXEC-018 production shape: joinAvailable + interpretBatch (joinAny still accepted).
  const source = [
    'module JoinTool',
    'let execute scope context =',
    '    let! recovery = scope.RequireFamilyRecovery root',
    '    match recovery with',
    '    | FamilyReady permit ->',
    '        let program = joinAvailable permit MaxJoinBatch interrupt.Wait',
    '        let! joined = JoinInterpreter.interpretBatch runtime program',
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
    'match! ChildRecoveryInterpreter.resolveAndCommit ports with',
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

test('P0_RECOVERY_JOIN_GATE_record_completion_owner_allowlist_is_green', () => {
  const owner = [
    'module ChildRecoveryInterpreter',
    'let commitJoinable journal parentId proof =',
    '    HandleController.recordCompletion journal parentId proof',
  ].join('\n')
  const def = [
    'module HandleController',
    'let recordCompletion journal parentId completion =',
    '    Ok ()',
  ].join('\n')
  assert.equal(scanText(owner, 'ChildRecoveryInterpreter.fs').filter((h) => h.id === 'record-completion-single-owner').length, 0)
  assert.equal(scanText(def, 'HandleController.fs').filter((h) => h.id === 'record-completion-single-owner').length, 0)
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
    '        ExecutorSummarize.summarizeSpool',
    '            (ExecutorSummarize.asExecutorRuntime runtime requirePermit)',
    '            spoolPath',
  ].join('\n')
  const summarize = [
    'module ExecutorSummarize',
    'let awaitAgent runtime agentId stash =',
    '    let! joined = runtime.JoinWithPermit(Some remainingMs)',
  ].join('\n')
  const wrap = [
    'module ExecutorSummarizeRuntime',
    'let asExecutorRuntime runtime requirePermit =',
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
  assert.ok(!scanText(summarize, 'ExecutorSummarize.fs').some((h) => h.id === 'executor-summarize-join-with-permit'))
  assert.ok(!scanText(summarize, 'ExecutorSummarize.fs').some((h) => h.id === 'tools-no-bare-runtime-join'))
  assert.ok(!scanText(wrap, 'ExecutorSummarizeRuntime.fs').some((h) => h.id === 'executor-runtime-join-with-permit'))
  assert.ok(!scanText(wrap, 'ExecutorSummarizeRuntime.fs').some((h) => h.id === 'tools-no-bare-runtime-join'))
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
    'src/Wanxiangshu/Domain/JoinProgram.fs',
    'src/Wanxiangshu/Domain/SessionRecovery.fs',
    'src/Wanxiangshu/Kernel/Fact.fs',
    'src/Wanxiangshu/Application/Reconciliation/ChildRecoveryInterpreter.fs',
    'src/Wanxiangshu/Application/Reconciliation/SessionRecoveryInterpreter.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Host/PluginRuntimeScope.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Plugin/SpikePlugin.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/JoinTool.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/ExecutorTool.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/ExecutorSummarize.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/ExecutorSummarizeRuntime.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Codec/JoinResultRenderer.fs',
  ]
  const entries = files.map((rel) => ({
    file: rel,
    text: readFileSync(join(ROOT, rel), 'utf8'),
  }))
  const hits = scanFiles(entries)
  assert.deepEqual(
    hits,
    [],
    hits.map((h) => `${h.id}@${h.file}:${h.line}`).join('; '),
  )
})

test('P0_RECOVERY_JOIN_GATE_positive_clean_break_shapes_present', () => {
  const agent = readFileSync(join(ROOT, 'src/Wanxiangshu/Session/AgentCompletion.fs'), 'utf8')
  const codec = readFileSync(join(ROOT, 'src/Wanxiangshu/Session/HandleCompletionCodec.fs'), 'utf8')
  const child = readFileSync(join(ROOT, 'src/Wanxiangshu/Domain/ChildRecovery.fs'), 'utf8')
  const mailbox = readFileSync(join(ROOT, 'src/Wanxiangshu/Session/CompletionMailbox.fs'), 'utf8')
  const ports = readFileSync(
    join(ROOT, 'src/Wanxiangshu/Application/Reconciliation/SessionRecoveryInterpreter.fs'),
    'utf8',
  )
  const joinProgram = readFileSync(join(ROOT, 'src/Wanxiangshu/Domain/JoinProgram.fs'), 'utf8')
  const fact = readFileSync(join(ROOT, 'src/Wanxiangshu/Kernel/Fact.fs'), 'utf8')

  for (const [file, text, ids] of [
    ['AgentCompletion.fs', agent, [
      'agent-outcome-completed-case',
      'agent-outcome-failed-case',
      'agent-outcome-abandoned-case',
      'agent-join-item-three-cases',
      'pty-aborted-retained',
    ]],
    ['HandleCompletionCodec.fs', codec, ['completion-blob-schema-v2', 'legacy-false-abort-decode']],
    ['ChildRecovery.fs', child, ['joinable-from-decoded', 'child-recovery-result-five-cases']],
    ['CompletionMailbox.fs', mailbox, ['mailbox-pulse-agent-handle', 'mailbox-publish-pty-completion']],
    [
      'SessionRecoveryInterpreter.fs',
      ports,
      ['session-ports-restore-handles-mandatory', 'session-ports-recover-jobs-mandatory'],
    ],
    ['JoinProgram.fs', joinProgram, ['join-program-requires-permit']],
    ['Fact.fs', fact, ['false-completion-rejected-fact', 'parent-join-correction-fact']],
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
