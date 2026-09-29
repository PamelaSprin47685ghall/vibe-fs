import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readFile } = await import("node:fs/promises");
const { default: test } = await import("node:test");

const read = (relative) => readFile(new URL(`../../../${relative}`, import.meta.url), 'utf8')

// WHAT[011]: termination only ever comes from an explicit causal event.
test('WHAT[speculative-investigation-011] SPEC_INV_011_replica_lifecycle_has_no_wall_clock_terminal_arbitration', async () => {
  const runtime = await read('src/Wanxiangshu/Strength/Replica/Runtime.fs')
  assert.doesNotMatch(runtime, /ITimerPort|timer\.Delay|completionWins|settleCompletionRace|maxLatencyMs|TimedOut/)
  assert.doesNotMatch(runtime, /\.IsCompleted|get_IsCompleted/)
  assert.match(runtime, /SemanticTerminal:\s*StrengthReplicaTerminal option/)

  const start = runtime.indexOf('member this.StartDecision')
  assert.ok(start > 0, 'the composed start capability is retained')
  const decision = runtime.slice(start, runtime.indexOf('member _.Dispose', start))
  assert.match(decision, /let!\s+result\s*=\s*prepared\.Completion/)
})
test('WHAT[speculative-investigation-011] SPEC_INV_011_the_prepared_stage_creates_the_child_without_sending_a_prompt', async () => {
  const runtime = await read('src/Wanxiangshu/Strength/Replica/Runtime.fs')
  const prepare = runtime.indexOf('member this.PrepareReplicaStart')
  const send = runtime.indexOf('member this.SendPreparedPrompt')
  assert.ok(prepare > 0 && send > prepare, 'the two stages are separate member boundaries')
  const preparedStage = runtime.slice(prepare, send)
  assert.match(preparedStage, /sessions\.CreateChildSession/)
  assert.doesNotMatch(preparedStage, /bootstrapDetachedSend/,
    'an empty child must not send a prompt or pre-occupy model capacity before DelegationBound is persisted')
  const sendStage = runtime.slice(send)
  assert.match(sendStage, /bootstrapDetachedSend/, 'only the send stage performs the bootstrap prompt')
  assert.match(runtime, /SendAgentOwnerRootWithTools/)
})
test('WHAT[speculative-investigation-011] SPEC_INV_011_model_reservation_stays_in_routing_while_the_bootstrap_send_is_model_free', async () => {
  const runtime = await read('src/Wanxiangshu/Strength/Replica/Runtime.fs')
  assert.match(
    runtime,
    /acquireOptionalModelOrAbort/,
    'the prepared replica still reserves its model through ModelRouting before the bootstrap send',
  )
  assert.doesNotMatch(
    runtime,
    /promptModel/,
    'the bootstrap send must not carry a model parameter; the reservation stays in ModelRouting',
  )
  const wiring = await read('src/Wanxiangshu/OpenCode/Plugin/PluginSessionWiring.fs')
  assert.match(wiring, /ModelRouting\.tryReserveManaged/, 'the reservation stays registered with ModelRouting')
  assert.match(wiring, /ModelExecutionPurpose\.ReadonlyDelegate/, 'the replica keeps its readonly-delegate purpose')
})
test('WHAT[speculative-investigation-011] SPEC_INV_011_runtime_has_no_production_dry_run_entry', async () => {
  const runtime = await read('src/Wanxiangshu/Strength/Replica/Runtime.fs')
  assert.doesNotMatch(runtime, /DryRun|dryRunStateAtTargetTerminal|StrengthReplicaPurpose/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => `H(${text})`
const opportunity = {
  isRootWork: true, requestKind: 'work-main', canonicalRole: 'engineer', ownerSessionId: 'owner',
  ownerLogicalRun: ['logical-1', 'authority-root-1'], sourcePhysicalUserMessageId: 'user-1',
  sourceProviderRun: 'run-1', sourceToolCallIds: ['call-1'], requestedRounds: 1, contractRevision: 1,
  hasPrefixProbe: false, isReplicaOrInternalLeaf: false, isInteractionRepair: false, isExplicitRecoveryBranch: false,
  ownerCancelled: false, targetProviderRunBound: true, eventStoreHealthy: true, hostBoundaryHealthy: true,
  processFuseHealthy: true, ownerLogicalRunSuperseded: false, pendingRequested: true, predictorConfigured: true,
}

// WHAT[011]/[014]: no environment variable, switch or artificial fingerprint can
// turn delegation on, off or into a dry run.
test('WHAT[speculative-investigation-011] STRENGTH_011_no_host_environment_variable_changes_the_delegation_decision', () => {
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
  const expected = Strength.policyDecide(H, opportunity)
  assert.equal(expected.kind, 'Admit')
  for (const name of ['WANXIANGSHU_STRENGTH_MODE', 'WANXIANGSHU_STRENGTH_DRY_RUN_BUDGET', 'WANXIANGSHU_STRENGTH_HOST_CANARY', 'WANXIANGSHU_STRENGTH_ENABLED']) {
    for (const value of ['dry-run', 'shadow', 'treatment', 'off', 'K1', 'K2', 'pass', '']) {
      withEnv(name, value, () => assert.deepEqual(Strength.policyDecide(H, opportunity), expected))
    }
  }
  for (const exportName of ['settingsLoad', 'settingsDryRunBudget', 'settingsHostCanaryHealthy', 'settingsHostCanaryFingerprint', 'startDryRun', 'observeDryRun', 'closeDryRunAtPrimaryTerminal']) {
    assert.equal(Strength[exportName], undefined, `no ${exportName} entry may remain`)
  }
})
test('WHAT[speculative-investigation-011] STRENGTH_011_process_fuse_is_first_failure_latched_and_cannot_be_cleared_by_a_session_cleanup', () => {
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
test('WHAT[speculative-investigation-011] STRENGTH_011_scope_dispose_drops_process_local_caches_but_never_untrips_the_fuse', () => {
  const scope = Strength.scopeCreate()
  const binding = Strength.runtimeBinding('owner-d', 'replica-d', 'dec-d', 'run-dec-d', 'Engineer', 1, 'sem-d', [])
  assert.equal(Strength.scopeRuntimeRegister(scope, binding).ok, true)
  assert.notEqual(Strength.scopeRuntimeFindByReplica(scope, 'replica-d'), null)
  Strength.scopeTripFuse(scope, 'boom')
  Strength.scopeDispose(scope)
  assert.equal(Strength.scopeRuntimeFindByReplica(scope, 'replica-d'), null)
  assert.equal(Strength.scopeFuseReason(scope), 'boom')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => `H(${text})`
const hostText = (text) => ({ type: 'text', text })
const hostResult = (callId, tool, input, output) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output } })
const user = (id, sessionId, parts) => ({ info: { id, role: 'user', sessionID: sessionId }, parts })
const assistant = (id, sessionId, parts) => ({ info: { id, role: 'assistant', sessionID: sessionId }, parts })
const binding = (owner, replica, decision, rounds) =>
  Strength.runtimeBinding(owner, replica, decision, `run-${decision}`, 'Engineer', rounds, `sem-${decision}`, [{ role: 'user', parts: [{ kind: 'text', text: 'owner mirror' }] }])
const attach = (replica, rounds, owner = 'owner') => {
  const handle = Strength.replicaRuntimeCreate()
  const decision = `decision-${replica}`
  const result = Strength.replicaAttach(handle, binding(owner, replica, decision, rounds))
  assert.equal(result.ok, true, result.error)
  return { handle, completion: result.value.completion }
}
const turn = (sessionId, outcome, providerRun = 'run-t') => ({ sessionId, providerRun, outcome, parts: [] })
const oneBatch = (replica) => ({ messages: [user('u1', replica, [hostText('Continue.')]), assistant('a1', replica, [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')])] })
const plainAnswer = (replica) => ({ messages: [user('u1', replica, [hostText('Continue.')]), assistant('a1', replica, [hostText('plain answer')])] })

test('WHAT[speculative-investigation-011] STRENGTH_011_replica_semantic_vs_physical_tail_lifecycle_split', () => {
  const runtime = Strength.runtimeCreate()
  const live = binding('owner-life', 'replica-life', 'd-life', 1)
  assert.equal(Strength.runtimeRegister(runtime, live).ok, true)
  assert.equal(Strength.runtimeFindByReplica(runtime, 'replica-life').decisionId, 'd-life')
  const retired = Strength.runtimeRetire(runtime, 'replica-life')
  assert.equal(retired.decisionId, 'd-life')
  assert.equal(Strength.runtimeFindByReplica(runtime, 'replica-life'), null)
})
test('WHAT[speculative-investigation-011] STRENGTH_011_semantic_terminal_is_first_wins_and_physical_tail_cannot_restart_business', async () => {
  const { handle, completion } = attach('replica-sem', 1)
  assert.equal(await Strength.replicaHandleTransform(handle, oneBatch('replica-sem')), true)
  const admitted = Strength.replicaPeek(handle, 'replica-sem')
  assert.equal(admitted.requestsAdmitted, 1)
  assert.equal(await Strength.replicaHandleTransform(handle, { messages: [
    user('u1', 'replica-sem', [hostText('Continue.')]),
    assistant('a1', 'replica-sem', [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')]),
    assistant('a2', 'replica-sem', [hostResult('c2', 'grep', { pattern: 'x' }, 'hit')]),
  ] }), true)
  const closed = Strength.replicaPeek(handle, 'replica-sem')
  assert.equal(closed.requestsAdmitted, 1, 'the refused outbound request never enters the count')
  assert.equal(closed.terminal.kind, 'BudgetReached')
  // The physical tail still has to be observed by the Host terminal.
  assert.equal(Strength.replicaHandleTurn(handle, turn('replica-sem', 'failed')), true)
  const outcome = await Strength.replicaAwaitOutcome(completion)
  assert.equal(outcome.terminal.kind, 'BudgetReached')
  assert.equal(outcome.requestsAdmitted, 1)
  assert.equal(Strength.replicaPeek(handle, 'replica-sem'), null)
  assert.equal(Strength.replicaLiveFind(handle, 'replica-sem'), null)
  assert.equal(Strength.replicaIsReplica(handle, 'replica-sem'), false)
  assert.equal(Strength.replicaHandleTurn(handle, turn('replica-sem', 'completed')), false)
  assert.deepEqual(Strength.replicaReleased(handle), ['replica-sem'])
})
test('WHAT[speculative-investigation-011] STRENGTH_011_session_delete_retires_live_and_orphan_bindings_with_one_lease_release', () => {
  const live = attach('replica-del', 1, 'owner-del')
  Strength.replicaSessionDeleted(live.handle, 'replica-del')
  assert.equal(Strength.replicaPeek(live.handle, 'replica-del'), null)
  assert.equal(Strength.replicaLiveFind(live.handle, 'replica-del'), null)
  assert.deepEqual(Strength.replicaReleased(live.handle), ['replica-del'])
  Strength.replicaSessionDeleted(live.handle, 'replica-del')
  assert.deepEqual(Strength.replicaReleased(live.handle), ['replica-del'])

  const orphan = Strength.replicaRuntimeCreate()
  assert.equal(Strength.replicaLiveRegister(orphan, binding('owner-orph', 'replica-orph', 'dec-orph', 1)).ok, true)
  assert.equal(Strength.replicaPeek(orphan, 'replica-orph'), null)
  Strength.replicaSessionDeleted(orphan, 'replica-orph')
  assert.equal(Strength.replicaLiveFind(orphan, 'replica-orph'), null)
  assert.deepEqual(Strength.replicaReleased(orphan), ['replica-orph'])

  const owned = attach('replica-owned', 1, 'owner-owned')
  Strength.replicaSessionDeleted(owned.handle, 'owner-owned')
  assert.equal(Strength.replicaPeek(owned.handle, 'replica-owned'), null)
  assert.deepEqual(Strength.replicaReleased(owned.handle), ['replica-owned'])
})
test('WHAT[speculative-investigation-011] STRENGTH_011_replica_dispose_keeps_first_terminal_and_clears_all_live_resources', async () => {
  const open = attach('replica-open', 1)
  Strength.replicaDispose(open.handle)
  const cancelled = await Strength.replicaAwaitOutcome(open.completion)
  assert.equal(cancelled.terminal.kind, 'Cancelled')
  assert.equal(Strength.replicaPeek(open.handle, 'replica-open'), null)
  assert.equal(Strength.replicaLiveFind(open.handle, 'replica-open'), null)
  assert.deepEqual(Strength.replicaReleased(open.handle), ['replica-open'])

  const closed = attach('replica-closed', 1)
  assert.equal(await Strength.replicaHandleTransform(closed.handle, plainAnswer('replica-closed')), true)
  assert.equal(Strength.replicaHandleTurn(closed.handle, {
    sessionId: 'replica-closed', providerRun: 'run-t', outcome: 'failed', parts: [{ kind: 'text', text: 'plain answer' }],
  }), true)
  const first = await Strength.replicaAwaitOutcome(closed.completion)
  assert.equal(first.terminal.kind, 'TextCompleted')
  Strength.replicaDispose(closed.handle)
  const kept = await Strength.replicaAwaitOutcome(closed.completion)
  assert.deepEqual(kept, first)
  assert.deepEqual(Strength.replicaReleased(closed.handle), ['replica-closed'])
})
test('WHAT[speculative-investigation-011] STRENGTH_011_owner_cancel_releases_the_replica_without_promoting_material', async () => {
  const { handle } = attach('replica-cancel', 1, 'owner-cancel')
  await Strength.replicaCancelOwner(handle, 'owner-cancel')
  assert.equal(Strength.replicaPeek(handle, 'replica-cancel'), null)
  assert.equal(Strength.replicaLiveFind(handle, 'replica-cancel'), null)
  assert.deepEqual(Strength.replicaReleased(handle), ['replica-cancel'])
})

}
