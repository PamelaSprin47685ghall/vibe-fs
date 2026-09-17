import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readFile } = await import("node:fs/promises");
const { default: test } = await import("node:test");

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
  const strengthPorts = await read('src/Wanxiangshu/OpenCode/Plugin/PluginStrengthPorts.fs')
  assert.match(runtime, /CancelOwner/)
  assert.match(runtime, /AbortSession/)
  assert.match(runtime, /CloseDryRunAtTargetTerminal/)
  assert.match(runtime, /dryRunStateAtTargetTerminal[\s\S]*TargetProviderRun = turn\.ProviderRun[\s\S]*StrengthReplicaPurpose\.DryRun/)
  // SPEC-INV-013: HostTurnObserver delegates pre-turn replica arbitration via handlePreTurn
  // before any other business observation is admitted.
  assert.match(
    observer,
    /let!\s+preTurnHandled\s*=\s*match\s+handlePreTurn\s+with[\s\S]*?if\s+preTurnHandled\s+then[\s\S]*?do!\s+XWire\.reconcileAttempt[\s\S]*?return\s+\(\)[\s\S]*?else/
  )
  // Target terminal close for observation-only DryRun is implemented in the port composition layer
  assert.match(strengthPorts, /runtime\.CloseDryRunAtTargetTerminal\s+turn/)

  const host = await read('src/Wanxiangshu/Strength/OpenCode/Speculate.fs')
  const dry = branch(host, '| StrengthRolloutMode.DryRun ->', '| StrengthRolloutMode.Off ->')
  assert.doesNotMatch(dry, /HostMessageProjection\.replaceMessagesInPlace/)
  assert.doesNotMatch(dry, /TripStrengthFuse\([^)]*TimedOut/i)
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const Fission = await import("../../../dist/Execution/Fission/Surface.js");
const authority = await import("../../../dist/Interaction/Authority/RuntimeSurface.js");
const persona = await import("../../../dist/Participant/Persona/Surface.js");

const H = (value) => `H(${value})`
const rootSelection = (agent) => {
  const resolved = persona.resolveParticipantIdentityAtRoot(agent)
  assert.equal(resolved.ok, true, resolved.ok ? '' : resolved.error)
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      selectedAgent: resolved.identity.name,
      peerAgent: resolved.identity.peer,
      canonicalRole: resolved.identity.role,
      selectedTier: resolved.identity.initialTier.toLowerCase(),
      persona: resolved.identity.persona,
      personaCatalogVersion: resolved.identity.catalogVersion,
      origin: resolved.identity.origin,
    },
  }
}
const ownerProfile = (agent = 'engineer') => {
  const result = authority.createAuthorityRoot(
    H,
    'runtime-special-lineage',
    'ses_special_owner',
    'HumanRoot',
    'msg_special_owner',
    rootSelection(agent),
  )
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result.value
}
const binding = (owner, replica, decision, role = 'Coder', budget = 'K1') => Strength.runtimeBinding(owner, replica, decision, `run-${decision}`, role, budget, 65536, `sem-${decision}`, [])
const hostText = (text) => ({ type: 'text', text })
const hostResult = (callId, tool, input, output) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output } })
const user = (id, sessionId, parts) => ({ info: { id, role: 'user', sessionID: sessionId }, parts })
const assistant = (id, sessionId, parts) => ({ info: { id, role: 'assistant', sessionID: sessionId }, parts })
const replicaBinding = (owner, replica, decision, budget) => Strength.runtimeBinding(owner, replica, decision, `run-${decision}`, 'Coder', budget, 65536, `sem-${decision}`, [{ role: 'user', parts: [{ kind: 'text', text: 'owner mirror' }] }])
const attach = (replica, budget, purpose = 'Treatment', owner = 'owner') => {
  const handle = Strength.replicaRuntimeCreate(65536)
  const decision = `decision-${replica}`
  const result = Strength.replicaAttach(handle, replicaBinding(owner, replica, decision, budget), purpose)
  assert.equal(result.ok, true, result.error)
  return { handle, completion: result.value.completion }
}
const turn = (sessionId, outcome, providerRun = 'run-t') => ({ sessionId, providerRun, outcome, parts: [] })
const oneBatch = (replica) => ({ messages: [user('u1', replica, [hostText('Continue.')]), assistant('a1', replica, [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')])] })

test('WHAT[SPEC-INV-013] STRENGTH_013_dry_run_closes_only_at_the_exact_owner_target_run', async () => {
  const handle = Strength.replicaRuntimeCreate(65536)
  const attached = Strength.replicaAttach(handle, replicaBinding('owner-dry', 'replica-dry', 'dec-dry', 'K1'), 'DryRun')
  assert.equal(attached.ok, true, attached.error)
  // A different run never closes the observation.
  await Strength.replicaCloseDryRun(handle, turn('owner-dry', 'completed', 'run-other'))
  assert.equal(Strength.replicaPeek(handle, 'replica-dry').terminal, null)
  // The exact owner target run closes it as Cancelled.
  await Strength.replicaCloseDryRun(handle, turn('owner-dry', 'completed', 'run-dec-dry'))
  const outcome = await Strength.replicaAwaitOutcome(attached.value.completion)
  assert.equal(outcome.terminal.kind, 'Cancelled')
})
}
