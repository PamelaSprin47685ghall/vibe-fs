import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })
const result = (callId, resultText) => ({ kind: 'tool-result', callId, result: resultText })
const text = (textValue) => ({ kind: 'text', text: textValue })
const msg = (role, parts) => ({ role, parts })

test('WHAT[SPEC-INV-003] STRENGTH_003_005_collector_preserves_provider_request_batches_and_concurrent_order', () => {
  const batches = Strength.collectCompleteBatches([
    msg('user', [text('root')]),
    msg('assistant', [call('c1', 'read', '{"a":1}'), call('c2', 'grep', '{"b":2}')]),
    msg('tool', [result('c2', 'two'), result('c1', 'one')]),
    msg('assistant', [call('c3', 'glob', '{}')]),
    msg('tool', [result('c3', 'three')]),
  ])
  assert.equal(batches.length, 2)
  assert.equal(batches[0].requestOrdinal, 1)
  assert.deepEqual(batches[0].exchanges.map((exchange) => exchange.toolName), ['read', 'grep'])
  assert.deepEqual(batches[0].exchanges.map((exchange) => exchange.canonicalResult), ['one', 'two'])
  assert.equal(batches[1].requestOrdinal, 2)
})
test('WHAT[SPEC-INV-003] STRENGTH_005_incomplete_batch_and_results_after_next_provider_message_are_not_collected', () => {
  assert.deepEqual(Strength.collectCompleteBatches([
    msg('assistant', [call('c1', 'read', '{}'), call('c2', 'grep', '{}')]),
    msg('tool', [result('c1', 'one')]),
  ]), [])
  assert.deepEqual(Strength.collectCompleteBatches([
    msg('assistant', [call('c1', 'read', '{}')]),
    msg('assistant', [text('next provider output')]),
    msg('tool', [result('c1', 'late')]),
  ]), [])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const Wire = await import("../../../dist/OpenCode/Codec/ProviderProjectionSurface.js");

const H = (text) => `H(${text})`
const hostText = (text) => ({ type: 'text', text })
const hostCall = (callId, tool, input) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output: 'pending' } })
const hostResult = (callId, tool, input, output) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output } })
const user = (id, sessionId, parts) => ({ info: { id, role: 'user', sessionID: sessionId }, parts })
const assistant = (id, sessionId, parts) => ({ info: { id, role: 'assistant', sessionID: sessionId }, parts })
const tool = (id, sessionId, parts) => ({ info: { id, role: 'tool', sessionID: sessionId }, parts })
const binding = (replica, budget) => Strength.runtimeBinding('owner', replica, `decision-${replica}`, `target-${replica}`, 'Coder', budget, 65536, `semantic-${replica}`, [{ role: 'user', parts: [{ kind: 'text', text: 'owner mirror' }] }])
const registered = (replica, budget) => {
  const runtime = Strength.runtimeCreate()
  assert.equal(Strength.runtimeRegister(runtime, binding(replica, budget)).ok, true)
  return runtime
}
const apply = async (runtime, output) => Strength.transformApply(H, runtime, output)

test('WHAT[SPEC-INV-003] STRENGTH_003_K1_aborts_before_provider_request_2_after_one_complete_batch', async () => {
  const runtime = registered('replica-k1', 'K1')
  const output = { messages: [user('u1', 'replica-k1', [hostText('Continue.')]), assistant('a1', 'replica-k1', [hostCall('c1', 'read', { filePath: 'a' })]), tool('t1', 'replica-k1', [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')])] }
  const outcome = await apply(runtime, output)
  assert.equal(outcome.kind, 'Retired')
  assert.equal(outcome.reason, 'provider-request-budget-reached')
  assert.equal(outcome.batches.length, 1)
  assert.deepEqual(outcome.aborted, ['replica-k1'])
  assert.notEqual(
    Strength.runtimeFindByReplica(runtime, 'replica-k1'),
    null,
    'K gate closes semantic admission but physical Replica identity lives until Host terminal/deletion',
  )
  assert.notEqual(Strength.runtimeRetire(runtime, 'replica-k1'), null)
  assert.equal((await apply(runtime, output)).kind, 'NotReplica')
})
test('WHAT[SPEC-INV-003] STRENGTH_003_multiple_parallel_tool_calls_in_one_request_form_single_batch', async () => {
  const runtime = registered('replica-multi-tool', 'K1')
  const output = {
    messages: [
      user('u1', 'replica-multi-tool', [hostText('Continue.')]),
      assistant('a1', 'replica-multi-tool', [
        hostResult('c1', 'read', { filePath: 'a' }, 'alpha'),
        hostResult('c2', 'grep', { pattern: 'x' }, 'beta'),
      ]),
    ]
  }
  const outcome = await apply(runtime, output)
  assert.equal(outcome.kind, 'Retired')
  assert.equal(outcome.reason, 'provider-request-budget-reached')
  assert.equal(outcome.batches.length, 1)
  assert.equal(outcome.batches[0].exchanges.length, 2)
  assert.equal(outcome.batches[0].exchanges[0].toolName, 'read')
  assert.equal(outcome.batches[0].exchanges[1].toolName, 'grep')
  assert.deepEqual(outcome.aborted, ['replica-multi-tool'])
})
test('WHAT[SPEC-INV-003] STRENGTH_003_K1_counts_OpenCode_completed_tool_part_as_one_real_request', async () => {
  const runtime = registered('replica-host-k1', 'K1')
  const output = { messages: [user('u1', 'replica-host-k1', [hostText('Continue.')]), assistant('a1', 'replica-host-k1', [hostResult('c1', 'read', { filePath: 'README.md' }, 'alpha')]), assistant('a2', 'replica-host-k1', [])] }
  const outcome = await apply(runtime, output)
  assert.equal(outcome.kind, 'Retired')
  assert.equal(outcome.reason, 'provider-request-budget-reached')
  assert.equal(outcome.batches.length, 1)
  assert.equal(outcome.batches[0].exchanges[0].toolName, 'read')
  assert.equal(outcome.batches[0].exchanges[0].canonicalArguments, '{"filePath":"README.md"}')
  assert.equal(outcome.batches[0].exchanges[0].canonicalResult, 'alpha')
  assert.deepEqual(outcome.aborted, ['replica-host-k1'])
})
test('WHAT[SPEC-INV-003] STRENGTH_003_K2_allows_request_2_then_aborts_before_request_3', async () => {
  const runtime = registered('replica-k2', 'K2')
  const first = { messages: [user('u1', 'replica-k2', [hostText('Continue.')]), assistant('a1', 'replica-k2', [hostResult('c1', 'grep', { pattern: 'x' }, 'a:1:x')])] }
  assert.equal((await apply(runtime, first)).kind, 'Ready')
  const second = { messages: [user('u1', 'replica-k2', [hostText('Continue.')]), assistant('a1', 'replica-k2', [hostResult('c1', 'grep', { pattern: 'x' }, 'a:1:x')]), assistant('a2', 'replica-k2', [hostResult('c2', 'glob', { pattern: '**/*.fs' }, 'a.fs')])] }
  const retired = await apply(runtime, second)
  assert.equal(retired.kind, 'Retired')
  assert.equal(retired.reason, 'provider-request-budget-reached')
  assert.equal(retired.batches.length, 2)
  assert.deepEqual(retired.aborted, ['replica-k2'])
})
test('WHAT[SPEC-INV-003] STRENGTH_003_speculation_completion_is_aligned_to_provider_step_boundary_without_wall_clock_inference', async () => {
  const runtime = registered('replica-step-bound', 'K1')
  const output = {
    messages: [
      user('u1', 'replica-step-bound', [hostText('Continue.')]),
      assistant('a1', 'replica-step-bound', [hostResult('c1', 'read', { filePath: 'src/file.fs' }, 'content')]),
    ]
  }
  const outcome = await apply(runtime, output)
  assert.equal(outcome.kind, 'Retired')
  assert.equal(outcome.reason, 'provider-request-budget-reached')
  assert.equal(outcome.batches.length, 1)
  assert.deepEqual(outcome.aborted, ['replica-step-bound'])
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

test('WHAT[SPEC-INV-003] STRENGTH_003_replica_request_counts_never_exceed_the_immutable_k_budget', async () => {
  // K1 with a two-batch transcript still reports at most one request.
  const k1 = attach('replica-k1c', 'K1')
  const two = { messages: [user('u1', 'replica-k1c', [hostText('Continue.')]), assistant('a1', 'replica-k1c', [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')]), assistant('a2', 'replica-k1c', [hostResult('c2', 'grep', { pattern: 'x' }, 'hit')])] }
  assert.equal(await Strength.replicaHandleTransform(k1.handle, two), true)
  assert.equal(Strength.replicaPeek(k1.handle, 'replica-k1c').requestsAdmitted, 1)
  assert.deepEqual(Strength.replicaReleased(k1.handle), [])
  // K2 with a three-batch transcript still reports at most two requests.
  const k2 = attach('replica-k2c', 'K2')
  const three = { messages: [user('u1', 'replica-k2c', [hostText('Continue.')]), assistant('a1', 'replica-k2c', [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')]), assistant('a2', 'replica-k2c', [hostResult('c2', 'grep', { pattern: 'x' }, 'hit')]), assistant('a3', 'replica-k2c', [hostResult('c3', 'glob', { pattern: '**/*.fs' }, 'a.fs')])] }
  assert.equal(await Strength.replicaHandleTransform(k2.handle, three), true)
  assert.equal(Strength.replicaPeek(k2.handle, 'replica-k2c').requestsAdmitted, 2)
  const outcome = await Strength.replicaAwaitOutcome(k2.completion)
  assert.equal(outcome.terminal.kind, 'BudgetReached')
  assert.equal(outcome.requestsAdmitted, 2)
})
}
