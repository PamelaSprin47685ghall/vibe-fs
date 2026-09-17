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
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const packageJson = JSON.parse(readFileSync(new URL('../../../package.json', import.meta.url), 'utf8'))
const withEnv = (name, value, run) => {
  const previous = process.env[name]
  try {
    if (value === undefined) delete process.env[name]
    else process.env[name] = value
    run()
  } finally {
    if (previous === undefined) delete process.env[name]
    else process.env[name] = previous
  }
}
const withCanary = (value, run) => withEnv('WANXIANGSHU_STRENGTH_HOST_CANARY', value, run)

test('WHAT[SPEC-INV-011] STRENGTH_011_dry_run_is_an_explicit_non_default_host_canary_mode', () => {
  withEnv('WANXIANGSHU_STRENGTH_MODE', undefined, () => assert.equal(Strength.settingsLoad().mode, 'Shadow'))
  withEnv('WANXIANGSHU_STRENGTH_MODE', 'dry-run', () => assert.equal(Strength.settingsLoad().mode, 'DryRun'))
})
test('WHAT[SPEC-INV-011] STRENGTH_011_dry_run_budget_defaults_to_k1_and_requires_explicit_k2_canary_opt_in', () => {
  withEnv('WANXIANGSHU_STRENGTH_DRY_RUN_BUDGET', undefined, () => assert.equal(Strength.settingsDryRunBudget(), 'K1'))
  withEnv('WANXIANGSHU_STRENGTH_DRY_RUN_BUDGET', 'K2', () => assert.equal(Strength.settingsDryRunBudget(), 'K2'))
  withEnv('WANXIANGSHU_STRENGTH_DRY_RUN_BUDGET', 'garbage', () => assert.equal(Strength.settingsDryRunBudget(), 'K1'))
})
test('WHAT[SPEC-INV-011] STRENGTH_011_host_canary_is_bound_to_the_pinned_OpenCode_and_plugin_contract', () => {
  const expected = `opencode-ai@${packageJson.devDependencies['opencode-ai']}|@opencode-ai/plugin@${packageJson.peerDependencies['@opencode-ai/plugin']}|strength-host-canary-v1`
  assert.equal(Strength.settingsHostCanaryFingerprint, expected)
  withCanary(undefined, () => assert.equal(Strength.settingsHostCanaryHealthy(), false))
  withCanary('true', () => assert.equal(Strength.settingsHostCanaryHealthy(), false))
  withCanary('pass', () => assert.equal(Strength.settingsHostCanaryHealthy(), false))
  withCanary(Strength.settingsHostCanaryFingerprint, () => assert.equal(Strength.settingsHostCanaryHealthy(), true))
})
test('WHAT[SPEC-INV-011] STRENGTH_011_process_fuse_is_first-failure-latched_and_cannot_be_cleared_by_a_session_cleanup', () => {
  const scope = Strength.scopeCreate()
  assert.equal(Strength.scopeFuseReason(scope), null)
  Strength.scopeTripFuse(scope, 'projection-conflict')
  assert.equal(Strength.scopeFuseReason(scope), 'projection-conflict')
  Strength.scopeTripFuse(scope, 'later-noise')
  assert.equal(Strength.scopeFuseReason(scope), 'projection-conflict')
  Strength.scopeClearSession(scope, 'owner')
  assert.equal(Strength.scopeFuseReason(scope), 'projection-conflict')
  Strength.scopeDispose(scope)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => `H(${text})`

test('WHAT[SPEC-INV-011] STRENGTH_011_scope_fuse_keeps_first_reason_across_clear_and_dispose', () => {
  const scope = Strength.scopeCreate()
  assert.equal(Strength.scopeFuseReason(scope), null)
  Strength.scopeTripFuse(scope, 'first-failure')
  Strength.scopeTripFuse(scope, 'later-noise')
  assert.equal(Strength.scopeFuseReason(scope), 'first-failure')
  Strength.scopeClearSession(scope, 'ses-a')
  assert.equal(Strength.scopeFuseReason(scope), 'first-failure')
  Strength.scopeDispose(scope)
  assert.equal(Strength.scopeFuseReason(scope), 'first-failure')
})
test('WHAT[SPEC-INV-011] STRENGTH_011_scope_dispose_drops_process_local_caches_but_never_untrips_the_fuse', () => {
  const scope = Strength.scopeCreate()
  const feature = Strength.scopeFeature(scope, 'ses-a', 'Coder', 1000)
  Strength.scopeArm(scope, 'ses-a', 'run-1', feature)
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-1', 'ReadonlyBatch'), null)
  const binding = Strength.runtimeBinding('owner-d', 'replica-d', 'dec-d', 'run-dec-d', 'Coder', 'K1', 65536, 'sem-d', [])
  assert.equal(Strength.scopeRuntimeRegister(scope, binding).ok, true)
  assert.notEqual(Strength.scopeRuntimeFindByReplica(scope, 'replica-d'), null)
  Strength.scopeTripFuse(scope, 'boom')
  Strength.scopeDispose(scope)
  // Every process-local cache is gone: live binding, collector episode,
  // recent-primary window and predictor evidence.
  assert.equal(Strength.scopeRuntimeFindByReplica(scope, 'replica-d'), null)
  assert.deepEqual(Strength.scopeBucket(scope, feature), { opportunities: 0, readonlyFirst: 0, secondObservations: 0, readonlySecond: 0 })
  assert.deepEqual(Strength.scopeFeature(scope, 'ses-a', 'Coder', 1000).recentPrimary, [])
  assert.equal(Strength.scopeObserve(scope, 'ses-a', 'run-1', 'ReadonlyBatch'), null)
  // The process-lifetime fuse survives dispose.
  assert.equal(Strength.scopeFuseReason(scope), 'boom')
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

test('WHAT[SPEC-INV-011] STRENGTH_015_replica_semantic_vs_physical_tail_lifecycle_split', () => {
  const runtime = Strength.runtimeCreate()
  const b = binding('owner-life', 'replica-life', 'd-life')
  assert.equal(Strength.runtimeRegister(runtime, b).ok, true)

  // Business decision resolves, replica is in live registry
  assert.equal(Strength.runtimeFindByReplica(runtime, 'replica-life').decisionId, 'd-life')

  // Exact physical tail cleanup removes it from live registry
  const retired = Strength.runtimeRetire(runtime, 'replica-life')
  assert.equal(retired.decisionId, 'd-life')

  // Once retired, presence is gone and business cannot restart from registry presence
  assert.equal(Strength.runtimeFindByReplica(runtime, 'replica-life'), null)
})
test('WHAT[SPEC-INV-011] STRENGTH_015_replica_semantic_terminal_is_first_wins_and_physical_tail_cannot_restart_business', async () => {
  const { handle, completion } = attach('replica-sem', 'K1')
  // The K gate retires semantic admission but the physical identity lives on.
  assert.equal(await Strength.replicaHandleTransform(handle, oneBatch('replica-sem')), true)
  const admitted = Strength.replicaPeek(handle, 'replica-sem')
  assert.equal(admitted.terminal.kind, 'BudgetReached')
  assert.ok(admitted.requestsAdmitted <= 1)
  // A duplicate terminal through the turn path is consumed as physical tail
  // only: the first outcome never changes.
  assert.equal(Strength.replicaHandleTurn(handle, turn('replica-sem', 'failed')), true)
  const outcome = await Strength.replicaAwaitOutcome(completion)
  assert.equal(outcome.terminal.kind, 'BudgetReached')
  assert.ok(outcome.requestsAdmitted <= 1)
  // Retired: no peek, no live binding, no restart, one lease release.
  assert.equal(Strength.replicaPeek(handle, 'replica-sem'), null)
  assert.equal(Strength.replicaLiveFind(handle, 'replica-sem'), null)
  assert.equal(Strength.replicaIsReplica(handle, 'replica-sem'), false)
  assert.equal(Strength.replicaHandleTurn(handle, turn('replica-sem', 'completed')), false)
  assert.deepEqual(Strength.replicaReleased(handle), ['replica-sem'])
})
test('WHAT[SPEC-INV-011] STRENGTH_015_session_delete_retires_live_and_orphan_bindings_with_one_lease_release', () => {
  // Live decision state: delete retires peek, binding and lease exactly once.
  const live = attach('replica-del', 'K1', 'Treatment', 'owner-del')
  Strength.replicaSessionDeleted(live.handle, 'replica-del')
  assert.equal(Strength.replicaPeek(live.handle, 'replica-del'), null)
  assert.equal(Strength.replicaLiveFind(live.handle, 'replica-del'), null)
  assert.deepEqual(Strength.replicaReleased(live.handle), ['replica-del'])
  Strength.replicaSessionDeleted(live.handle, 'replica-del')
  assert.deepEqual(Strength.replicaReleased(live.handle), ['replica-del'])
  // Orphan binding with no local decision state: delete still retires and releases.
  const orphan = Strength.replicaRuntimeCreate(65536)
  assert.equal(Strength.replicaLiveRegister(orphan, replicaBinding('owner-orph', 'replica-orph', 'dec-orph', 'K1')).ok, true)
  assert.equal(Strength.replicaPeek(orphan, 'replica-orph'), null)
  Strength.replicaSessionDeleted(orphan, 'replica-orph')
  assert.equal(Strength.replicaLiveFind(orphan, 'replica-orph'), null)
  assert.deepEqual(Strength.replicaReleased(orphan), ['replica-orph'])
  // Owner deletion cascades to its live replica.
  const owned = attach('replica-owned', 'K1', 'Treatment', 'owner-owned')
  Strength.replicaSessionDeleted(owned.handle, 'owner-owned')
  assert.equal(Strength.replicaPeek(owned.handle, 'replica-owned'), null)
  assert.equal(Strength.replicaLiveFind(owned.handle, 'replica-owned'), null)
  assert.deepEqual(Strength.replicaReleased(owned.handle), ['replica-owned'])
})
test('WHAT[SPEC-INV-011] STRENGTH_015_replica_dispose_keeps_first_terminal_and_clears_all_live_resources', async () => {
  // Dispose before any terminal completes the open decision as Cancelled.
  const open = attach('replica-open', 'K1')
  Strength.replicaDispose(open.handle)
  const cancelled = await Strength.replicaAwaitOutcome(open.completion)
  assert.equal(cancelled.terminal.kind, 'Cancelled')
  assert.equal(Strength.replicaPeek(open.handle, 'replica-open'), null)
  assert.equal(Strength.replicaLiveFind(open.handle, 'replica-open'), null)
  assert.deepEqual(Strength.replicaReleased(open.handle), ['replica-open'])
  // Dispose after a terminal keeps the first terminal fixed.
  const closed = attach('replica-closed', 'K1')
  assert.equal(Strength.replicaHandleTurn(closed.handle, turn('replica-closed', 'completed')), true)
  const first = await Strength.replicaAwaitOutcome(closed.completion)
  assert.equal(first.terminal.kind, 'TextCompleted')
  Strength.replicaDispose(closed.handle)
  const kept = await Strength.replicaAwaitOutcome(closed.completion)
  assert.deepEqual(kept, first)
  assert.deepEqual(Strength.replicaReleased(closed.handle), ['replica-closed'])
})
}
