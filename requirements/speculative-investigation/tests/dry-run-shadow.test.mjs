// FROZEN — 2026-08-14. Written before implementation by explicit user request.
// Intentionally NOT executed before implementation.
//
// SPEC-INV-013: DryRun is real + OpenCode-visible + nonblocking + zero semantic promotion.

import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const read = (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

const branch = (source, start, end) => {
  const from = source.indexOf(start)
  assert.ok(from >= 0, `missing branch ${start}`)
  const to = source.indexOf(end, from + start.length)
  assert.ok(to > from, `missing end branch ${end}`)
  return source.slice(from, to)
}

test('WHAT[SPEC-INV-013] SPEC_INV_013_DryRun_owner_path_starts_shadow_and_does_not_await_replica_terminal', async () => {
  const source = await read('src/Wanxiangshu/Strength/OpenCode/Speculate.fs')
  const dry = branch(source, '| StrengthRolloutMode.DryRun ->', '| StrengthRolloutMode.Off ->')

  assert.match(dry, /applyDryRun|StartDryRun|startDryRun/)
  assert.doesNotMatch(dry, /let!\s+outcome\s*=\s*runtime\.StartDecision/)
  assert.doesNotMatch(dry, /StrengthCandidatePrepared|PublishPrepared|renderCandidate/)
  assert.doesNotMatch(dry, /StrengthCandidatePromoted|Promoted/)
})

test('WHAT[SPEC-INV-013] SPEC_INV_013_DryRun_runtime_creates_a_real_visible_attached_child_then_observes_it_independently', async () => {
  const source = await read('src/Wanxiangshu/Strength/Replica/Runtime.fs')
  const start = source.indexOf('member this.StartDryRun')
  assert.ok(start >= 0, 'runtime must expose a distinct StartDryRun capability')
  const nextMember = source.indexOf('member private _.StartReplica', start + 1)
  const dry = source.slice(start, nextMember > start ? nextMember : undefined)

  assert.match(dry, /this\.StartReplica/)
  assert.match(source, /CreateChildSession/)
  assert.match(source, /registerReplica/)
  assert.match(source, /SendAgentOwnerRootWithTools/)
  assert.match(source, /Detached/)
  assert.match(source, /ObserveDryRun/)

  // The returned start capability may await physical child creation/bootstrap, but never a terminal race.
  const returnBoundary = dry.search(/return\s+Ok/)
  assert.ok(returnBoundary > 0, 'StartDryRun must return a start handle/result')
  const beforeReturn = dry.slice(0, returnBoundary)
  assert.doesNotMatch(beforeReturn, /completionWins|deadline\.Delay|let!\s+result\s*=\s*.*Completion\.Task/)

  const observeStart = source.indexOf('member private _.ObserveDryRun')
  const observeEnd = source.indexOf('member this.StartDryRun', observeStart)
  const observe = source.slice(observeStart, observeEnd)
  assert.match(observe, /let!\s+_\s*=\s*state\.Completion\.Task/)
  assert.doesNotMatch(observe, /Delay|deadline|timeout|TimedOut/i)
})

test('WHAT[SPEC-INV-013] SPEC_INV_013_DryRun_terminal_only_ends_observation_and_owner_cancel_still_cascades', async () => {
  const runtime = await read('src/Wanxiangshu/Strength/Replica/Runtime.fs')
  const observer = await read('src/Wanxiangshu/OpenCode/Host/HostTurnObserver.fs')
  assert.match(runtime, /CancelOwner/)
  assert.match(runtime, /AbortSession/)
  assert.match(runtime, /CloseDryRunAtTargetTerminal/)
  assert.match(runtime, /dryRunStateAtTargetTerminal[\s\S]*TargetProviderRun = turn\.ProviderRun[\s\S]*StrengthReplicaPurpose\.DryRun/)
  assert.match(observer, /CloseDryRunAtTargetTerminal turn/)

  const host = await read('src/Wanxiangshu/Strength/OpenCode/Speculate.fs')
  const dry = branch(host, '| StrengthRolloutMode.DryRun ->', '| StrengthRolloutMode.Off ->')
  assert.doesNotMatch(dry, /HostMessageProjection\.replaceMessagesInPlace/)
  assert.doesNotMatch(dry, /TripStrengthFuse\([^)]*TimedOut/i)
})

test('WHAT[SPEC-INV-011] SPEC_INV_011_Strength_replica_lifecycle_has_no_wall_clock_terminal_arbitration', async () => {
  const runtime = await read('src/Wanxiangshu/Strength/Replica/Runtime.fs')
  assert.doesNotMatch(runtime, /ITimerPort|timer\.Delay|completionWins|settleCompletionRace|maxLatencyMs|TimedOut/)
  assert.doesNotMatch(runtime, /\.IsCompleted|get_IsCompleted/)
  assert.match(runtime, /SemanticTerminal:\s*StrengthReplicaTerminal option/)

  const start = runtime.indexOf('member this.StartDecision')
  assert.ok(start >= 0, 'Treatment must expose StartDecision')
  const decision = runtime.slice(start, runtime.indexOf('member _.Dispose', start))
  assert.match(decision, /let!\s+result\s*=\s*state\.Completion\.Task/)
})

test('WHAT[SPEC-INV-013] SPEC_INV_013_DryRun_visibility_is_not_a_fake_diagnostic_only_path', async () => {
  const runtime = await read('src/Wanxiangshu/Strength/Replica/Runtime.fs')
  const host = await read('src/Wanxiangshu/Strength/OpenCode/Speculate.fs')

  assert.match(runtime, /CreateChildSession/)
  assert.match(runtime, /StrengthReplicaBinding/)
  assert.match(runtime, /registerReplica/)
  assert.match(host, /StrengthRolloutMode\.DryRun/)
  assert.doesNotMatch(branch(host, '| StrengthRolloutMode.DryRun ->', '| StrengthRolloutMode.Off ->'), /fake|simulate|synthetic replica/i)
})
