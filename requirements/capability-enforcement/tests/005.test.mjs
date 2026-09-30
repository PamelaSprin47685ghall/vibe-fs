import assert from 'node:assert/strict'
import test from 'node:test'
import { capabilities, exactReadonlyHostToolMap, isAllowedTool } from '../../../dist/Strength/Surface.js'

test('WHAT[capability-enforcement-005] replica projections and tool policy allow only the three read-only operations', () => {
  assert.deepEqual(exactReadonlyHostToolMap, [
    { tool: '*', allowed: false },
    { tool: 'glob', allowed: true },
    { tool: 'grep', allowed: true },
    { tool: 'read', allowed: true },
  ])
  for (const role of ['engineer', 'devops']) {
    assert.deepEqual(capabilities(role), ['Glob', 'Grep', 'Read'])
  }
  assert.deepEqual(capabilities('manager'), [])
  for (const tool of ['read', 'glob', 'grep']) assert.equal(isAllowedTool(tool), true, tool)
  for (const tool of ['write', 'edit', 'run', 'fork', 'resume', 'join', 'network', 'bash', 'horizon', 'fission', 'unknown']) {
    assert.equal(isAllowedTool(tool), false, tool)
  }
})

test('WHAT[capability-enforcement-005] H12_replica_context_with_positive_estimate_and_self_note_strictly_denies_write_and_execution_tools', () => {
  // WHAT[005] / H12: estimated_readonly_rounds and self_note are strictly factual estimates,
  // NEVER execution permissions. In a StrengthReplica runtime context, attempting write/edit/run/fork/join/mcp
  // tools must be strictly denied by the capability gate with zero physical effects.
  const writeAndExecTools = [
    { tool: 'write', args: { filePath: 'src/foo.fs', content: 'let x = 1', estimated_readonly_rounds: 3, self_note: 'verify before write' } },
    { tool: 'edit', args: { filePath: 'src/foo.fs', oldString: 'a', newString: 'b', estimated_readonly_rounds: 2, self_note: 'verify before edit' } },
    { tool: 'run', args: { command: 'npm test', estimated_readonly_rounds: 1, self_note: 'run verification' } },
    { tool: 'fork', args: { role: 'engineer', estimated_readonly_rounds: 2, self_note: 'sub-task' } },
    { tool: 'join', args: { session: 'ses-1', estimated_readonly_rounds: 1, self_note: 'join session' } },
    { tool: 'mcp__filesystem__write_file', args: { path: '/tmp/test', estimated_readonly_rounds: 1, self_note: 'mcp write' } },
    { tool: 'js-engineer', args: { program: 'class Js {}', estimated_readonly_rounds: 1, self_note: 'js write' } },
    { tool: 'bash', args: { command: 'echo 1', estimated_readonly_rounds: 1, self_note: 'bash run' } },
  ]

  // Host tool execution gate lookup: exactReadonlyHostToolMap ends with wildcard deny (*: false)
  const isPermittedByHostGate = (toolName) => {
    const exact = exactReadonlyHostToolMap.find((entry) => entry.tool === toolName)
    if (exact) return exact.allowed
    const wildcard = exactReadonlyHostToolMap.find((entry) => entry.tool === '*')
    return wildcard?.allowed ?? false
  }

  for (const call of writeAndExecTools) {
    // 1. isAllowedTool gate strictly denies non-readonly tools regardless of positive estimates
    assert.equal(isAllowedTool(call.tool), false, `tool ${call.tool} must be denied by isAllowedTool gate`)
    // 2. exactReadonlyHostToolMap strictly denies non-readonly tools
    assert.equal(isPermittedByHostGate(call.tool), false, `tool ${call.tool} must be denied by exactReadonlyHostToolMap`)
  }

  // Allowed read-only tool triad remains strictly {read, glob, grep}
  for (const tool of ['read', 'glob', 'grep']) {
    assert.equal(isAllowedTool(tool), true)
    assert.equal(isPermittedByHostGate(tool), true)
  }
})

test('WHAT[capability-enforcement-005] H13_source_batch_participating_edit_or_run_never_expands_replica_tools_beyond_three_readonly', () => {
  // WHAT[005] / H13: The fact that participating source tools were 'edit' or 'run' in the source batch
  // does not expand Replica tools. After a source batch containing edit/run with positive estimates,
  // Replica capabilities and tool mappings remain strictly {read, glob, grep}.
  const sourceBatchParticipatingCalls = [
    { tool: 'edit', args: { filePath: 'src/lib.fs', oldString: 'x', newString: 'y', estimated_readonly_rounds: 3, self_note: 'check usages' } },
    { tool: 'run', args: { command: 'dotnet test', estimated_readonly_rounds: 2, self_note: 'run test suite' } },
  ]

  // Neither edit nor run may ever leak into Replica allowed tools
  for (const sourceCall of sourceBatchParticipatingCalls) {
    assert.equal(isAllowedTool(sourceCall.tool), false, `source tool ${sourceCall.tool} must never be allowed in replica`)
  }

  // For both engineer and devops roles, capabilities under strength-replica remain strictly Glob, Grep, Read
  for (const role of ['engineer', 'devops']) {
    assert.deepEqual(capabilities(role), ['Glob', 'Grep', 'Read'])
  }

  // exactReadonlyHostToolMap remains the exact 4-rule canonical map
  assert.deepEqual(exactReadonlyHostToolMap, [
    { tool: '*', allowed: false },
    { tool: 'glob', allowed: true },
    { tool: 'grep', allowed: true },
    { tool: 'read', allowed: true },
  ])
})
