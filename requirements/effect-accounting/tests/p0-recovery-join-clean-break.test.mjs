// Split from tests/unit/verify/p0-recovery-join-gate.test.mjs (cutover Wave 2a); owner: effect-accounting
//
// P0-RECOVERY-JOIN-001 §10 静态 gate — aborted≠terminal / clean-break 规则侧
// （EFFECT-ACCOUNTING-007，EXEC-020..024）：aborted 不是完成终态——无 Aborted case、
// 无 aborted 工厂、无 tryFromDurableCompleted、completion blob schema v2 等。
// recovery 规则归 crash-reconciliation；LOOP-006 桥接静态形状归 degeneration-guard。
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import {
  RULE_IDS,
  scanFiles,
  scanText,
} from '../../../scripts/checks/p0-recovery-join.mjs'

const ROOT = new URL('../../../', import.meta.url).pathname

/** aborted≠terminal / clean-break 规则（effect-accounting owner）。 */
const EFFECT_RULES = new Set([
  'agent-aborted-type',
  'agent-completion-aborted-factory',
  'child-run-make-aborted',
  'aborted-run-factory',
  'try-from-durable-completed',
  'publish-completion-agent',
  'lifecycle-aborted-completion',
  'host-fork-restart-false-finality',
  'host-fork-restart-bare-publish',
  'fork-runtime-parent-cancelled-aborted',
  'agent-outcome-completed-case',
  'agent-outcome-failed-case',
  'agent-outcome-abandoned-case',
  'agent-join-item-three-cases',
  'pty-aborted-retained',
  'completion-blob-schema-v2',
  'legacy-false-abort-decode',
  'codec-encode-finality-aborted',
  'join-renderer-agent-status-aborted',
  'false-completion-rejected-fact',
  'parent-join-correction-fact',
])

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
    id: 'lifecycle-aborted-completion',
    file: 'HostForkRunLifecycle.fs',
    source: [
      'module HostForkRunLifecycle',
      'let bad run =',
      '    AgentCompletion.aborted run.AgentId run.RunId None None "X" "y"',
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
]

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_GATE_exports_clean_break_rule_ids', () => {
  for (const id of EFFECT_RULES) {
    assert.ok(RULE_IDS.includes(id), `missing clean-break rule id: ${id}`)
  }
})

for (const sample of NEGATIVES) {
  test(`WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_GATE_negative_${sample.id}_goes_red`, () => {
    const hits = scanText(sample.source, sample.file)
    const ofId = hits.filter((h) => h.id === sample.id)
    assert.ok(ofId.length >= 1, `expected rule ${sample.id} to fire; got ${hits.map((h) => h.id).join(',')}`)
  })
}

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_GATE_production_sources_are_green', () => {
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
    'src/Wanxiangshu/Composition/Durable/Fact.fs',
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
  const hits = scanFiles(entries).filter((h) => EFFECT_RULES.has(h.id))
  assert.deepEqual(
    hits,
    [],
    hits.map((h) => `${h.id}@${h.file}:${h.line}`).join('; '),
  )
})

test('WHAT[EFFECT-ACCOUNTING-007] P0_RECOVERY_JOIN_GATE_positive_clean_break_shapes_present', () => {
  const agent = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/AgentCompletion.fs'), 'utf8')
  const codec = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Handle/CompletionCodec.fs'), 'utf8')
  const fact = readFileSync(join(ROOT, 'src/Wanxiangshu/Composition/Durable/Fact.fs'), 'utf8')

  for (const [file, text, ids] of [
    ['AgentCompletion.fs', agent, [
      'agent-outcome-completed-case',
      'agent-outcome-failed-case',
      'agent-outcome-abandoned-case',
      'agent-join-item-three-cases',
      'pty-aborted-retained',
    ]],
    ['HandleCompletionCodec.fs', codec, ['completion-blob-schema-v2', 'legacy-false-abort-decode']],
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
