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
]

test('P0_RECOVERY_JOIN_GATE_exports_rule_ids', () => {
  assert.ok(RULE_IDS.includes('join-tool-family-recovery'))
  assert.ok(RULE_IDS.includes('lifecycle-aborted-completion'))
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
    '    match! runtime.Join() with',
    '    | Ok c -> encode c',
  ].join('\n')
  const hits = scanText(source, 'JoinTool.fs')
  assert.ok(hits.some((h) => h.id === 'join-tool-family-recovery'))
})

test('P0_RECOVERY_JOIN_GATE_join_tool_with_require_stays_green_for_positive', () => {
  const source = [
    'module JoinTool',
    'let execute scope context =',
    '    let! recovery = scope.RequireFamilyRecovery root',
    '    match recovery with',
    '    | FamilyReady _ -> match! runtime.Join() with',
    '    | FamilyBlocked b -> recoveryBlocked b',
  ].join('\n')
  const hits = scanText(source, 'JoinTool.fs')
  assert.ok(!hits.some((h) => h.id === 'join-tool-family-recovery'))
})

test('P0_RECOVERY_JOIN_GATE_production_sources_are_green', () => {
  const files = [
    'src/Wanxiangshu/Session/HostForkRunLifecycle.fs',
    'src/Wanxiangshu/Session/ForkRecovery.fs',
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
