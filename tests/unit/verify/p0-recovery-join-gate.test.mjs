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
  assert.ok(RULE_IDS.includes('lifecycle-aborted-completion'))
  assert.ok(RULE_IDS.includes('record-completion-single-owner'))
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
  const source = [
    'module JoinTool',
    'let execute scope context =',
    '    let! recovery = scope.RequireFamilyRecovery root',
    '    match recovery with',
    '    | FamilyReady permit ->',
    '        let program = JoinProgram.joinAny permit',
    '        let! joined = JoinInterpreter.interpret runtime program',
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
  const source = [
    'module HostForkRestart',
    'match! ChildRecoveryInterpreter.resolveAndCommit ports with',
    '| Ok (Joinable proof) -> ()',
    'match JoinableCompletion.tryFromDurableCompleted agentId handle child kind body with',
    '| Ok _ -> runtime.PublishCompletion completion',
  ].join('\n')
  const hits = scanText(source, 'HostForkRestart.fs')
  assert.ok(!hits.some((h) => h.id === 'host-fork-restart-proof-structure'))
  assert.ok(!hits.some((h) => h.id === 'host-fork-restart-false-finality'))
  assert.ok(!hits.some((h) => h.id === 'host-fork-restart-bare-publish'))
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

test('P0_RECOVERY_JOIN_GATE_production_sources_are_green', () => {
  const files = [
    'src/Wanxiangshu/Session/HostForkRunLifecycle.fs',
    'src/Wanxiangshu/Session/ForkRecovery.fs',
    'src/Wanxiangshu/Session/HostForkRestart.fs',
    'src/Wanxiangshu/Session/ForkRuntime.fs',
    'src/Wanxiangshu/Session/HandleController.fs',
    'src/Wanxiangshu/Application/Reconciliation/ChildRecoveryInterpreter.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Host/PluginRuntimeScope.fs',
    'src/Wanxiangshu/Infrastructure/OpenCode/Tools/JoinTool.fs',
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
