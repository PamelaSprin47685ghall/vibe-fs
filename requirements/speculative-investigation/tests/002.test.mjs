import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => text
const opportunity = {
  isRootWork: true,
  requestKind: 'work-main',
  canonicalRole: 'engineer',
  ownerSessionId: 'owner',
  ownerLogicalRun: ['logical-1', 'authority-root-1'],
  sourcePhysicalUserMessageId: 'user-1',
  sourceProviderRun: 'run-1',
  sourceToolCallIds: ['call-1'],
  requestedRounds: 3,
  contractRevision: 1,
  hasPrefixProbe: false,
  isReplicaOrInternalLeaf: false,
  isInteractionRepair: false,
  isExplicitRecoveryBranch: false,
  ownerCancelled: false,
  targetProviderRunBound: true,
  eventStoreHealthy: true,
  hostBoundaryHealthy: true,
  processFuseHealthy: true,
  ownerLogicalRunSuperseded: false,
  pendingRequested: true,
  predictorConfigured: true,
}
const admit = (overrides) => {
  const result = Strength.policyDecide(H, { ...opportunity, ...overrides })
  if (result.kind !== 'Admit') assert.fail(`expected Admit, got Skip ${result.reason}`)
  return result.request
}
const skipReason = (overrides) => {
  const result = Strength.policyDecide(H, { ...opportunity, ...overrides })
  if (result.kind !== 'Skip') assert.fail('expected Skip')
  return result.reason
}

// WHAT[002]: one batch of integers collapses to one maximum. Nothing is
// normalized, averaged or silently dropped.
test('WHAT[speculative-investigation-002] STRENGTH_002_round_budget_rejects_negative_values_without_normalizing_to_zero', () => {
  assert.deepEqual(Strength.budgetTryCreate(0), { ok: true, value: 0 })
  assert.deepEqual(Strength.budgetTryCreate(7), { ok: true, value: 7 })
  const negative = Strength.budgetTryCreate(-1)
  assert.equal(negative.ok, false)
  assert.equal(negative.error, 'negative-readonly-round-budget')
  assert.equal(Strength.budgetTryCreate(-3).ok, false)
})
test('WHAT[speculative-investigation-002] STRENGTH_002_round_max_collapses_the_owner_batch_to_its_largest_integer', () => {
  assert.deepEqual(Strength.budgetMaxOf([1, 2, 3]), { ok: true, value: 3 })
  assert.deepEqual(Strength.budgetMaxOf([3, 1, 2]), { ok: true, value: 3 })
  assert.deepEqual(Strength.budgetMaxOf([0, 0, 0]), { ok: true, value: 0 })
})
test('WHAT[speculative-investigation-002] STRENGTH_002_round_batch_without_declaration_is_distinct_from_zero_rounds', () => {
  // An empty batch grants no authorization opportunity at all; zero rounds is
  // a real declaration that must still be rejected before any send.
  const empty = Strength.budgetMaxOf([])
  assert.equal(empty.ok, true)
  assert.equal(empty.value, null)
  assert.notEqual(empty.value, 0)
})
test('WHAT[speculative-investigation-002] STRENGTH_002_illegal_value_refuses_the_whole_batch_instead_of_half_delegating', () => {
  const illegal = Strength.budgetMaxOf([2, -1, 4])
  assert.equal(illegal.ok, false)
  assert.equal(illegal.error, 'negative-readonly-round-budget')
})
// DELEGATE 14.1 / WHAT[002]: one provider response carries a mixed read/edit
// tool set. The current batch still runs exactly once, and the delegation that
// may follow it is decided by the batch maximum. A zero declared inside the
// same batch is an ordinary value, never a veto.
test('WHAT[speculative-investigation-002] STRENGTH_002_mixed_read_edit_batch_runs_once_then_delegates_by_batch_max', () => {
  const mixedCalls = [
    { callId: 'c1', name: 'read', rounds: 0 },
    { callId: 'c2', name: 'edit', rounds: 2 },
    { callId: 'c3', name: 'grep', rounds: 5 },
  ]
  const call = (entry) => ({
    kind: 'tool-call',
    callId: entry.callId,
    name: entry.name,
    args: JSON.stringify({ delegate_readonly_rounds: entry.rounds }),
  })
  const msg = (role, parts) => ({ role, parts })
  const batches = Strength.collectCompleteBatches([
    msg('user', [{ kind: 'text', text: 'root' }]),
    msg('assistant', mixedCalls.map(call)),
    msg('tool', mixedCalls.map((entry) => ({ kind: 'tool-result', callId: entry.callId, result: `${entry.name} done` }))),
  ])

  // The mixed batch is one request and runs once, in the original call order.
  assert.equal(batches.length, 1, 'a mixed read/edit batch is one request, never several rounds or delegations')
  assert.deepEqual(batches[0].exchanges.map((exchange) => exchange.toolName), ['read', 'edit', 'grep'])
  assert.equal(batches[0].requestOrdinal, 1)

  // The budgets travel with the calls that really executed.
  const budgets = batches[0].exchanges.map((exchange) => JSON.parse(exchange.canonicalArguments).delegate_readonly_rounds)
  assert.deepEqual(budgets, [0, 2, 5])

  // One rule, one collapse: the batch maximum. The same-batch zero is a value.
  const max = Strength.budgetMaxOf(budgets)
  assert.deepEqual(max, { ok: true, value: 5 })
  assert.notEqual(max.value, 0, 'a zero inside the batch never vetoes the positive rounds')

  // Where the zero sits changes nothing: no second rule per position.
  assert.deepEqual(Strength.budgetMaxOf([2, 5, 0]), { ok: true, value: 5 })
  assert.deepEqual(Strength.budgetMaxOf([5, 0]), { ok: true, value: 5 })

  // The delegation that may follow the executed batch sees exactly that maximum.
  const admitted = Strength.policyDecide(H, { ...opportunity, requestedRounds: max.value })
  assert.equal(admitted.kind, 'Admit')
  assert.equal(admitted.request.requestedRounds, 5)
})
test('WHAT[speculative-investigation-002] STRENGTH_002_admission_refuses_every_documented_dependency_gap_with_a_visible_reason', () => {
  assert.equal(skipReason({ isRootWork: false }), 'not-root-work')
  assert.equal(skipReason({ requestKind: 'strength-replica' }), 'not-work-main')
  assert.equal(skipReason({ isReplicaOrInternalLeaf: true }), 'replica-or-internal-leaf')
  assert.equal(skipReason({ hasPrefixProbe: true }), 'prefix-probe')
  assert.equal(skipReason({ ownerCancelled: true }), 'owner-cancelled')
  assert.equal(skipReason({ targetProviderRunBound: false }), 'target-provider-run-unbound')
  assert.equal(skipReason({ eventStoreHealthy: false }), 'event-store-unhealthy')
  assert.equal(skipReason({ hostBoundaryHealthy: false }), 'host-boundary-unhealthy')
  assert.equal(skipReason({ processFuseHealthy: false }), 'process-fuse-unhealthy')
  assert.equal(skipReason({ ownerLogicalRunSuperseded: true }), 'owner-logical-run-superseded')
  assert.equal(skipReason({ pendingRequested: false }), 'no-pending-requested')
  assert.equal(skipReason({ predictorConfigured: false }), 'predictor-unconfigured')
  assert.equal(skipReason({ sourceToolCallIds: [] }), 'empty-source-tool-call-set')
  assert.equal(skipReason({ requestedRounds: 0 }), 'zero-round-budget')
  assert.equal(skipReason({ requestedRounds: -2 }), 'requested-rounds-out-of-range')
  assert.equal(skipReason({ requestedRounds: null }), 'no-authorization-opportunity')
  assert.equal(skipReason({ requestedRounds: 'K1' }), 'requested-rounds-out-of-range')
  assert.equal(skipReason({ requestedRounds: 2.5 }), 'requested-rounds-out-of-range')
})
test('WHAT[speculative-investigation-002] STRENGTH_002_unknown_role_or_request_kind_is_fail_closed_not_a_default_admission', () => {
  for (const field of ['canonicalRole', 'requestKind']) {
    const result = Strength.policyDecide(H, { ...opportunity, [field]: 'unknown' })
    assert.equal(result.ok, false)
    assert.match(result.error, /unknown (role|request kind)/)
  }
})
test('WHAT[speculative-investigation-002] STRENGTH_002_admitted_request_is_minimal_protocol_evidence_without_pricing_or_scores', () => {
  const request = admit()
  assert.equal(request.requestedRounds, 3)
  assert.equal(request.contractRevision, 1)
  assert.equal(request.sourceProviderRun, 'run-1')
  assert.deepEqual(request.ownerLogicalRun, { logicalRunId: 'logical-1', authorityRootUserMessageId: 'authority-root-1' })
  assert.deepEqual(request.sourceToolCallIds, ['call-1'])
  for (const forbidden of ['budget', 'value', 'estimate', 'prediction', 'score', 'cost', 'tier']) {
    assert.equal(forbidden in request, false, `admission must not carry ${forbidden}`)
  }
})
test('WHAT[speculative-investigation-002] STRENGTH_002_batch_collector_counts_only_complete_provider_request_batches', () => {
  const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })
  const result = (callId, resultText) => ({ kind: 'tool-result', callId, result: resultText })
  const text = (textValue) => ({ kind: 'text', text: textValue })
  const msg = (role, parts) => ({ role, parts })
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
})
test('WHAT[speculative-investigation-002] STRENGTH_002_incomplete_batch_and_results_after_next_provider_message_are_not_collected', () => {
  const call = (callId, name, args) => ({ kind: 'tool-call', callId, name, args })
  const result = (callId, resultText) => ({ kind: 'tool-result', callId, result: resultText })
  const text = (textValue) => ({ kind: 'text', text: textValue })
  const msg = (role, parts) => ({ role, parts })
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
const { readFileSync } = await import("node:fs");
const { resolve } = await import("node:path");
const { default: test } = await import("node:test");

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[speculative-investigation-002] StrengthDelegate owns the readonly delegation entry point in the composition root', () => {
  const delegate = read('src/Wanxiangshu/Strength/OpenCode/Delegate.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(delegate, /let\s+tryApply/)
  assert.match(delegate, /let\s+tryCapture/)
  assert.match(delegate, /let\s+private\s+publishAndRender/)
  assert.match(delegate, /let\s+private\s+consumeBoundDecision/)
  // The presentation path must never capture unconfirmed material into the
  // canonical XTrace timeline; replay/capture lives in StrengthReplay.
  assert.doesNotMatch(delegate, /XTrace/)
  assert.doesNotMatch(delegate, /DryRun|StrengthRollout|costEstimate|predictorPredict/i)
  assert.match(pt, /StrengthDelegate\.tryApply/)
})
// WHAT[speculative-investigation-002]: the composition root's explicit
// replica/ordinary transform mode used to be guarded here by matching the
// source text of the private TransformMode declaration. That was a source-
// shape assertion, not a behavior test: it pinned a private DU and failed on
// a legitimate refactor that retired one case. structured-workflow [017]
// forbids a self-built static scan of PluginTransforms as a second truth;
// the pipeline's behavior must be proven by the host-boundary ordered-
// transform contract tests. That proof exists: requirements/host-boundary/
// tests/019.test.mjs drives the production createWithCaps seam and asserts
// the ordinary canonical sequence (which reaches the readonly delegation
// entry) and the replica branch (replica steps only, no normal steps).
// The proof stays there.
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => `H(${text})`
const opportunity = {
  isRootWork: true, requestKind: 'work-main', canonicalRole: 'engineer', ownerSessionId: 'owner',
  ownerLogicalRun: { logicalRunId: 'logical-1', authorityRootUserMessageId: 'authority-root-1' },
  sourcePhysicalUserMessageId: 'user-1', sourceProviderRun: 'run-1', sourceToolCallIds: ['call-1'],
  contractRevision: 1, hasPrefixProbe: false, isReplicaOrInternalLeaf: false, isInteractionRepair: false,
  isExplicitRecoveryBranch: false, ownerCancelled: false, targetProviderRunBound: true, eventStoreHealthy: true,
  hostBoundaryHealthy: true, processFuseHealthy: true, ownerLogicalRunSuperseded: false, pendingRequested: true,
  predictorConfigured: true,
}

// DELEGATE 14.1 / WHAT[002]+[003]: the round budget is the owner's own integer,
// exhaustive over its declared range. Zero is a real declaration (no
// delegation), one is a real round, the schema maximum is a real budget, and
// anything outside the declared range refuses instead of being normalized.
// 032 C18 owns the parameter-boundary codes; this is the behavior side.
test('WHAT[speculative-investigation-002] STRENGTH_002_budget_range_is_exhaustive_and_never_normalized', () => {
  // Construction accepts the whole declared range, both boundaries included.
  for (const value of [0, 1, 2, 5, 2147483646, 2147483647]) {
    assert.deepEqual(
      Strength.budgetTryCreate(value),
      { ok: true, value },
      `a declared budget of ${value} must be accepted verbatim`,
    )
  }
  // Negative values refuse; nothing is silently turned into zero rounds.
  for (const value of [-1, -2, -2147483648]) {
    const refused = Strength.budgetTryCreate(value)
    assert.equal(refused.ok, false, `budget ${value} must be refused`)
    assert.equal(refused.error, 'negative-readonly-round-budget')
    assert.equal(refused.value, undefined, 'a refused budget never produces a value')
  }

  // One batch collapses to its maximum, boundaries included.
  assert.deepEqual(Strength.budgetMaxOf([0, 1, 5]), { ok: true, value: 5 })
  assert.deepEqual(Strength.budgetMaxOf([1, 5, 2]), { ok: true, value: 5 })
  assert.deepEqual(
    Strength.budgetMaxOf([2147483647, 0]),
    { ok: true, value: 2147483647 },
    'the schema maximum survives the batch collapse')
  assert.deepEqual(Strength.budgetMaxOf([0, 0, 0]), { ok: true, value: 0 })
  assert.equal(Strength.budgetMaxOf([]).value, null, 'an empty batch grants no opportunity at all')
  // One illegal value refuses the whole batch; no half-normalized max survives.
  const illegal = Strength.budgetMaxOf([2, -1, 4])
  assert.equal(illegal.ok, false)
  assert.equal(illegal.error, 'negative-readonly-round-budget')

  // Behavior side: the admitted authorization carries the declared maximum and
  // nothing re-caps it at the admission layer.
  for (const rounds of [1, 5, 2147483647]) {
    const admitted = Strength.policyDecide(H, { ...opportunity, requestedRounds: rounds })
    assert.equal(admitted.kind, 'Admit', `a declared budget of ${rounds} must be admitted`)
    assert.equal(admitted.request.requestedRounds, rounds, 'the authorization carries the declared maximum')
  }
  // Zero is a legitimate declaration, distinct from an unconfigured Predictor.
  const zero = Strength.policyDecide(H, { ...opportunity, requestedRounds: 0 })
  assert.equal(zero.kind, 'Skip')
  assert.equal(zero.reason, 'zero-round-budget')
  // Missing is a third state, distinct from both.
  const missing = Strength.policyDecide(H, { ...opportunity, requestedRounds: null })
  assert.equal(missing.kind, 'Skip')
  assert.equal(missing.reason, 'no-authorization-opportunity')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const Strength = await import("../../../dist/Strength/Surface.js");

const H = (text) => `H(${text})`
const malformedIdentity = 'malformed-owner-logical-run'
const decodedIdentity = { logicalRunId: 'logical-1', authorityRootUserMessageId: 'authority-root-1' }

// WHAT[002] + WHAT[006]: the owner logical run names the authority an
// authorization would consume. The surface receives that identity in the two
// shapes that each describe it — a frozen request carries the named fields, an
// admission opportunity carries the [logicalRunId; authorityRootUserMessageId]
// pair. Every other shape describes no identity at all, and Policy never
// inspects the identity's content: it would derive a real DecisionId from
// empty strings and append a DelegationRequested with no authority behind it.
// So the surface must refuse at its own boundary instead of decoding nothing
// into an empty identity.
const legalIdentities = {
  'admission pair': ['logical-1', 'authority-root-1'],
  'frozen-request named fields': { logicalRunId: 'logical-1', authorityRootUserMessageId: 'authority-root-1' },
}

const malformedIdentities = {
  'absent': undefined,
  'null': null,
  'scalar string': 'logical-1',
  'short pair': ['logical-1'],
  'long pair': ['logical-1', 'authority-root-1', 'extra'],
  'empty logical run': ['', 'authority-root-1'],
  'blank logical run': ['   ', 'authority-root-1'],
  'empty named logical run': { logicalRunId: '', authorityRootUserMessageId: 'authority-root-1' },
  'missing named authority root': { logicalRunId: 'logical-1' },
  'non-string named authority root': { logicalRunId: 'logical-1', authorityRootUserMessageId: 7 },
}

const opportunityBase = {
  isRootWork: true, requestKind: 'work-main', canonicalRole: 'engineer', ownerSessionId: 'owner',
  ownerLogicalRun: decodedIdentity, sourcePhysicalUserMessageId: 'user-1',
  sourceProviderRun: 'run-1', sourceToolCallIds: ['call-1'], requestedRounds: 1, contractRevision: 1,
  hasPrefixProbe: false, isReplicaOrInternalLeaf: false, isInteractionRepair: false,
  isExplicitRecoveryBranch: false, ownerCancelled: false, targetProviderRunBound: true,
  eventStoreHealthy: true, hostBoundaryHealthy: true, processFuseHealthy: true,
  ownerLogicalRunSuperseded: false, pendingRequested: true, predictorConfigured: true,
}

const requestBase = {
  decisionId: 'decision-1', ownerSessionId: 'owner', ownerLogicalRun: decodedIdentity,
  sourcePhysicalUserMessageId: 'user-1', sourceProviderRun: 'run-1',
  sourceToolCallIds: ['call-1'], requestedRounds: 2, contractRevision: 1,
}

test('WHAT[speculative-investigation-002] STRENGTH_002_a_malformed_owner_identity_never_becomes_an_admission', () => {
  for (const [shape, ownerLogicalRun] of Object.entries(malformedIdentities)) {
    const refused = Strength.policyDecide(H, { ...opportunityBase, ownerLogicalRun })
    assert.equal(refused.ok, false, `admission must not open for a ${shape} owner logical run`)
    assert.equal(refused.error, malformedIdentity, `${shape} must be named, not decoded into an empty identity`)
    assert.equal(refused.kind, undefined, 'a decode failure is not an admission skip with a reason')
  }
})

test('WHAT[speculative-investigation-002] STRENGTH_002_a_malformed_owner_identity_never_becomes_a_frozen_request_or_event', () => {
  for (const [shape, ownerLogicalRun] of Object.entries(malformedIdentities)) {
    for (const entry of [{ name: 'delegationRequest', build: (value) => Strength.delegationRequest(value) },
                         { name: 'eventRequested', build: (value) => Strength.eventRequested(value) }]) {
      const refused = entry.build({ ...requestBase, ownerLogicalRun })
      assert.equal(refused.ok, false, `${entry.name} must not freeze a ${shape} owner logical run`)
      assert.equal(refused.error, malformedIdentity, `${entry.name} must name ${shape} instead of recording an empty identity`)
    }
  }
})

test('WHAT[speculative-investigation-002] STRENGTH_002_a_decodable_owner_identity_survives_the_guard_in_both_shapes', () => {
  for (const [shape, ownerLogicalRun] of Object.entries(legalIdentities)) {
    const admitted = Strength.policyDecide(H, { ...opportunityBase, ownerLogicalRun })
    assert.equal(admitted.ok, undefined, `the guard must not refuse the ${shape} identity`)
    assert.equal(admitted.kind, 'Admit', `the ${shape} identity is a legal authority`)
    assert.deepEqual(admitted.request.ownerLogicalRun, decodedIdentity,
      `the ${shape} must decode to the authority it describes, never to an empty identity`)

    const lifecycle = Strength.delegationRequest({ ...requestBase, ownerLogicalRun })
    assert.equal(lifecycle.ok, undefined, `the guard must not refuse the ${shape} identity at the frozen-request boundary`)
    assert.equal(lifecycle.error, undefined)
    assert.equal(Strength.delegationDecisionId(lifecycle), 'decision-1',
      'a decodable identity still folds into the same authorization')

    const event = Strength.eventRequested({ ...requestBase, ownerLogicalRun })
    assert.equal(event.ok, undefined, `the guard must not refuse the ${shape} identity at the event boundary`)
    assert.equal(event.error, undefined)
  }
})
}
