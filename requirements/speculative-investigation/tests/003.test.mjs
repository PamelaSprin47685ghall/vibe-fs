import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const Wire = await import("../../../dist/OpenCode/Codec/ProviderProjectionSurface.js");

const H = (text) => `H(${text})`
const hostText = (text) => ({ type: 'text', text })
const hostResult = (callId, tool, input, output) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output } })
const user = (id, sessionId, parts) => ({ info: { id, role: 'user', sessionID: sessionId }, parts })
const assistant = (id, sessionId, parts) => ({ info: { id, role: 'assistant', sessionID: sessionId }, parts })
const tool = (id, sessionId, parts) => ({ info: { id, role: 'tool', sessionID: sessionId }, parts })

const mirror = [{ role: 'user', parts: [{ kind: 'text', text: 'owner mirror' }] }]
const binding = (owner, replica, decision, rounds) =>
  Strength.runtimeBinding(owner, replica, decision, `run-${decision}`, 'Engineer', rounds, `sem-${decision}`, mirror)

// Handle path: the runtime admission itself drives the gate, exactly as the
// Host transform does on every real outbound request boundary.
const attach = (replica, rounds, owner = 'owner') => {
  const handle = Strength.replicaRuntimeCreate()
  const decision = `decision-${replica}`
  const result = Strength.replicaAttach(handle, binding(owner, replica, decision, rounds))
  assert.equal(result.ok, true, result.error)
  return { handle, decision, completion: result.value.completion }
}
const oneBatch = (replica) => ({ messages: [user('u1', replica, [hostText('Continue.')]), assistant('a1', replica, [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')])] })
const twoBatches = (replica) => ({ messages: [
  user('u1', replica, [hostText('Continue.')]),
  assistant('a1', replica, [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')]),
  assistant('a2', replica, [hostResult('c2', 'grep', { pattern: 'x' }, 'hit')]),
] })
const threeBatches = (replica) => ({ messages: [
  user('u1', replica, [hostText('Continue.')]),
  assistant('a1', replica, [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')]),
  assistant('a2', replica, [hostResult('c2', 'grep', { pattern: 'x' }, 'hit')]),
  assistant('a3', replica, [hostResult('c3', 'glob', { pattern: '**/*.fs' }, 'a.fs')]),
] })
test('WHAT[speculative-investigation-003] STRENGTH_003_repeated_notification_for_one_admitted_request_does_not_add_a_round', async () => {
  const { handle } = attach('replica-idem', 1)
  assert.equal(await Strength.replicaHandleTransform(handle, oneBatch('replica-idem')), true)
  assert.equal(Strength.replicaPeek(handle, 'replica-idem').requestsAdmitted, 1)
  assert.equal(await Strength.replicaHandleTransform(handle, oneBatch('replica-idem')), true)
  assert.equal(Strength.replicaPeek(handle, 'replica-idem').requestsAdmitted, 1)
  assert.equal(await Strength.replicaHandleTransform(handle, oneBatch('replica-idem')), true)
  assert.equal(Strength.replicaPeek(handle, 'replica-idem').requestsAdmitted, 1)
})
test('WHAT[speculative-investigation-003] STRENGTH_003_second_request_is_refused_before_any_send_when_the_round_is_spent', async () => {
  const { handle } = attach('replica-n1', 1)
  assert.equal(await Strength.replicaHandleTransform(handle, oneBatch('replica-n1')), true)
  assert.equal(await Strength.replicaHandleTransform(handle, twoBatches('replica-n1')), true)
  const peek = Strength.replicaPeek(handle, 'replica-n1')
  assert.equal(peek.requestsAdmitted, 1, 'the refused outbound request must never enter the admission count')
  assert.equal(peek.terminal.kind, 'BudgetReached')
  // The already completed rounds stay folded: refusing the next outbound
  // request discards nothing that was truthfully observed by the replica.
  // The refused request never enters the admission count; the exchanges the
  // child really performed are still real material.
  assert.equal(peek.batches.length, 2)
  // Semantic gate closes, the physical child identity still lives until the
  // Host terminal: refusal of one request is not a deletion.
  assert.notEqual(Strength.replicaLiveFind(handle, 'replica-n1'), null)
  assert.equal(Strength.replicaIsReplica(handle, 'replica-n1'), true)
})
test('WHAT[speculative-investigation-003] STRENGTH_003_every_admitted_round_collects_its_tool_results_before_the_gate_closes', async () => {
  const { handle } = attach('replica-n2', 2)
  assert.equal(await Strength.replicaHandleTransform(handle, oneBatch('replica-n2')), true)
  assert.equal(await Strength.replicaHandleTransform(handle, twoBatches('replica-n2')), true)
  const admitted = Strength.replicaPeek(handle, 'replica-n2')
  assert.equal(admitted.requestsAdmitted, 2)
  assert.equal(admitted.batches.length, 2)
  assert.equal(await Strength.replicaHandleTransform(handle, threeBatches('replica-n2')), true)
  const closed = Strength.replicaPeek(handle, 'replica-n2')
  assert.equal(closed.requestsAdmitted, 2)
  assert.equal(closed.terminal.kind, 'BudgetReached')
})
test('WHAT[speculative-investigation-003] STRENGTH_003_plain_text_ending_consumes_a_real_round', async () => {
  const { handle, completion } = attach('replica-text', 1)
  assert.equal(await Strength.replicaHandleTransform(handle, { messages: [user('u1', 'replica-text', [hostText('readonly assignment')])] }), true)
  assert.equal(Strength.replicaPeek(handle, 'replica-text').requestsAdmitted, 1)
  assert.equal(Strength.replicaHandleTurn(handle, {
    sessionId: 'replica-text', physicalUserMessageId: 'u1', providerRun: 'run-t', outcome: 'completed', parts: [{ kind: 'text', text: 'plain answer, no tools' }],
  }), true)
  const outcome = await Strength.replicaAwaitOutcome(completion)
  assert.equal(outcome.terminal.kind, 'TextCompleted')
  assert.equal(outcome.requestsAdmitted, 1)
})
test('WHAT[speculative-investigation-003] STRENGTH_003_repeated_terminal_notification_leaves_the_first_outcome_untouched', async () => {
  const { handle, completion } = attach('replica-tail', 1)
  assert.equal(await Strength.replicaHandleTransform(handle, oneBatch('replica-tail')), true)
  assert.equal(Strength.replicaHandleTurn(handle, {
    sessionId: 'replica-tail', physicalUserMessageId: 'u1', providerRun: 'run-t', outcome: 'failed', parts: [],
  }), true)
  const first = await Strength.replicaAwaitOutcome(completion)
  // The host reports a failed physical turn for this admitted round: it still
  // consumed one real round and still ends this decision. `Abandoned` is a
  // domain-layer consumption close, not a replica execution terminal.
  assert.equal(first.terminal.kind, 'Failed')
  assert.equal(first.requestsAdmitted, 1)
  // A second terminal observation for the same physical tail adds no round and
  // cannot rewrite the immutable first outcome.
  assert.equal(Strength.replicaHandleTurn(handle, {
    sessionId: 'replica-tail', physicalUserMessageId: 'u1', providerRun: 'run-t', outcome: 'completed', parts: [],
  }), false)
  const again = await Strength.replicaAwaitOutcome(completion)
  assert.deepEqual(again, first)
  assert.deepEqual(Strength.replicaReleased(handle), ['replica-tail'])
})

// DELEGATE 14.1 / WHAT[003]: a round that was really dispatched stays consumed
// when the provider reports it as failed. A failure is not a refund, and the
// closed decision leaves no second round behind for a free retry.
test('WHAT[speculative-investigation-003] STRENGTH_003_a_dispatched_round_that_failed_still_consumes_its_round', async () => {
  const { handle, completion } = attach('replica-failed-round', 2)
  assert.equal(await Strength.replicaHandleTransform(handle, oneBatch('replica-failed-round')), true)
  assert.equal(Strength.replicaPeek(handle, 'replica-failed-round').requestsAdmitted, 1)

  assert.equal(Strength.replicaHandleTurn(handle, {
    sessionId: 'replica-failed-round', physicalUserMessageId: 'u1', providerRun: 'run-failed', outcome: 'failed', parts: [],
  }), true)
  const failed = await Strength.replicaAwaitOutcome(completion)
  assert.equal(failed.terminal.kind, 'Failed')
  assert.equal(failed.requestsAdmitted, 1, 'the failed round is not refunded')

  // The closed decision leaves no second round behind for a free retry.
  assert.equal(Strength.replicaPeek(handle, 'replica-failed-round'), null)
  assert.equal(Strength.replicaLiveFind(handle, 'replica-failed-round'), null)

  // A repeated terminal notification for the same physical tail adds no round
  // and cannot rewrite the immutable first outcome.
  assert.equal(Strength.replicaHandleTurn(handle, {
    sessionId: 'replica-failed-round', physicalUserMessageId: 'u1', providerRun: 'run-failed', outcome: 'completed', parts: [],
  }), false)
  const again = await Strength.replicaAwaitOutcome(completion)
  assert.deepEqual(again, failed)
})

// DELEGATE 14.1 / WHAT[003]: used rounds are a durable fact of the admitted
// execution, never a view measurement. A shortened history (Host truncation or
// ordinary compaction) must not rebuild a smaller count and re-open a spent
// round.
test('WHAT[speculative-investigation-003] STRENGTH_003_shortened_history_never_rebuilds_a_smaller_round_count', async () => {
  const { handle } = attach('replica-short-view', 3)
  // Each round is a distinct physical request: the request key carries the
  // latest assistant response's identity, so every round must present a new
  // assistant id (or it is the same request re-transformed, not a new round).
  const roundBatch = (round) => ({ messages: [
    user('u1', 'replica-short-view', [hostText('Continue.')]),
    assistant(`a-round-${round}`, 'replica-short-view', [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')]),
  ] })
  for (let round = 1; round <= 3; round += 1) {
    assert.equal(await Strength.replicaHandleTransform(handle, roundBatch(round)), true)
  }
  assert.equal(Strength.replicaPeek(handle, 'replica-short-view').requestsAdmitted, 3)

  // Only the latest exchange is still visible, as after truncation or ordinary
  // compaction. The used round count must not be rebuilt from that shrunken view.
  const shortened = { messages: [
    user('u-short', 'replica-short-view', [hostText('Continue.')]),
    assistant('a-short', 'replica-short-view', [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')]),
  ] }
  assert.equal(await Strength.replicaHandleTransform(handle, shortened), true)
  const closed = Strength.replicaPeek(handle, 'replica-short-view')
  assert.equal(closed.requestsAdmitted, 3, 'a shortened view must not shrink the used round count')
  assert.equal(closed.terminal.kind, 'BudgetReached')
})
}


{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");
const Wire = await import("../../../dist/OpenCode/Codec/ProviderProjectionSurface.js");

const H = (text) => `H(${text})`
const hostText = (text) => ({ type: 'text', text })
const hostResult = (callId, tool, input, output) => ({ type: 'tool', tool, callID: callId, state: { status: 'completed', input, output } })
const user = (id, sessionId, parts) => ({ info: { id, role: 'user', sessionID: sessionId }, parts })
const assistant = (id, sessionId, parts) => ({ info: { id, role: 'assistant', sessionID: sessionId }, parts })
const tool = (id, sessionId, parts) => ({ info: { id, role: 'tool', sessionID: sessionId }, parts })
const binding = (replica, rounds) =>
  Strength.runtimeBinding('owner', replica, `decision-${replica}`, `target-${replica}`, 'Engineer', rounds, `semantic-${replica}`, [{ role: 'user', parts: [{ kind: 'text', text: 'owner mirror' }] }])
const registered = (replica, rounds) => {
  const runtime = Strength.runtimeCreate()
  assert.equal(Strength.runtimeRegister(runtime, binding(replica, rounds)).ok, true)
  return runtime
}

test('WHAT[speculative-investigation-003] resident history cannot be republished or stop collection of a new decision', async () => {
  const replica = 'resident-new-decision'
  const runtime = registered(replica, 2)
  const transformed = await Strength.transformApply(H, runtime, { messages: [
    user('old-assignment', replica, [hostText('previous readonly assignment')]),
    assistant('old-read', replica, [hostResult('old-call', 'js-predictor', { code: 'old' }, 'stale-evidence')]),
    assistant('old-final', replica, [hostText('previous decision completed')]),
    user('new-assignment', replica, [hostText('current readonly assignment')]),
    assistant('new-read', replica, [hostResult('new-call', 'js-predictor', { code: 'new' }, 'current-evidence')]),
  ] }, true)
  assert.equal(transformed.kind, 'Ready')
  assert.deepEqual(transformed.batches.map(batch => batch.exchanges.map(exchange => exchange.canonicalResult)), [['current-evidence']])
})

test('WHAT[speculative-investigation-003] STRENGTH_003_outbound_gate_is_the_admission_not_the_visible_batch_count', async () => {
  const runtime = registered('replica-gate', 1)
  const admitted = await Strength.transformApply(H, runtime, { messages: [
    user('u1', 'replica-gate', [hostText('Continue.')]),
    assistant('a1', 'replica-gate', [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')]),
  ] }, true)
  assert.equal(admitted.kind, 'Ready')
  assert.equal(admitted.batches.length, 1)

  const refused = await Strength.transformApply(H, runtime, { messages: [
    user('u1', 'replica-gate', [hostText('Continue.')]),
    assistant('a1', 'replica-gate', [hostResult('c1', 'read', { filePath: 'a' }, 'alpha')]),
    assistant('a2', 'replica-gate', [hostResult('c2', 'grep', { pattern: 'x' }, 'hit')]),
  ] }, true)
  assert.equal(refused.kind, 'Retired')
  assert.equal(refused.reason, 'provider-request-budget-reached')
  assert.deepEqual(refused.aborted, ['replica-gate'])
  assert.notEqual(
    Strength.runtimeFindByReplica(runtime, 'replica-gate'),
    null,
    'the gate closes semantic admission while the physical identity still lives until the Host terminal',
  )
  assert.equal((await Strength.transformApply(H, runtime, { messages: [] }, false)).kind, 'NotReplica')
})
test('WHAT[speculative-investigation-003] STRENGTH_004_non_replica_session_returns_not_replica', async () => {
  const runtime = registered('replica-known', 1)
  const outcome = await Strength.transformApply(H, runtime, { messages: [user('u1', 'unknown-session', [hostText('Continue.')])] }, false)
  assert.equal(outcome.kind, 'NotReplica')
  assert.deepEqual(outcome.batches, [])
  assert.deepEqual(outcome.aborted, [])
})
test('WHAT[speculative-investigation-003] STRENGTH_003_multiple_parallel_tool_calls_in_one_request_form_single_batch', async () => {
  const runtime = registered('replica-multi-tool', 1)
  const outcome = await Strength.transformApply(H, runtime, { messages: [
    user('u1', 'replica-multi-tool', [hostText('Continue.')]),
    assistant('a1', 'replica-multi-tool', [
      hostResult('c1', 'read', { filePath: 'a' }, 'alpha'),
      hostResult('c2', 'grep', { pattern: 'x' }, 'beta'),
    ]),
  ] }, true)
  assert.equal(outcome.kind, 'Ready')
  assert.equal(outcome.batches.length, 1)
  assert.equal(outcome.batches[0].exchanges.length, 2)
  assert.equal(outcome.batches[0].exchanges[0].toolName, 'read')
  assert.equal(outcome.batches[0].exchanges[1].toolName, 'grep')
})
test('WHAT[speculative-investigation-003] STRENGTH_003_completed_host_tool_part_is_one_real_request_with_canonical_material', async () => {
  const runtime = registered('replica-host', 1)
  const outcome = await Strength.transformApply(H, runtime, { messages: [
    user('u1', 'replica-host', [hostText('Continue.')]),
    assistant('a1', 'replica-host', [hostResult('c1', 'read', { filePath: 'README.md' }, 'alpha')]),
  ] }, true)
  assert.equal(outcome.kind, 'Ready')
  assert.equal(outcome.batches[0].exchanges[0].toolName, 'read')
  assert.equal(outcome.batches[0].exchanges[0].canonicalArguments, '{"filePath":"README.md"}')
  assert.equal(outcome.batches[0].exchanges[0].canonicalResult, 'alpha')
})
}
