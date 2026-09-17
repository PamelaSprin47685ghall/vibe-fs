import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const cycle = await import("../../../dist/Context/Companion/Blogger/Runtime/CycleSurface.js");

const materialize = (over = {}) => ({ kind: 'materialize', requestId: 'req-1', blogger: 'ses-blogger', digest: 'ctx-1', ...over })
const entry = (over = {}) => ({ kind: 'entry', requestId: 'req-1', run: 'msg-e1', ...over })
const squash = (over = {}) => ({ kind: 'squash', requestId: 'req-s1', run: 'msg-s1', ...over })
const state = (...actions) => cycle.scenario(actions)
const ok = (...actions) => {
  const result = state(...actions)
  assert.equal(result.ok, true, result.error ?? '')
  return result.state
}

test('WHAT[EFFECT-ACCOUNTING-004] C5_same_request_materialize_is_idempotent', () => {
  assert.deepEqual(ok(materialize({ requestId: 'req-idem' }), materialize({ requestId: 'req-idem' })), {
    openRequests: 1,
    openBloggers: 1,
    receipts: 0,
    requestBindings: 0,
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { budget, providerFailureProjection } = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");
const handles = await import("../../../dist/Execution/Delegation/Handle/Surface.js");

const OWNER = budget.attemptIdentity('ses_mgr', 'run_L', 'msg_u1', 'run_owner')
const BLOGGER = budget.attemptIdentity('ses_mgr', 'run_L', 'msg_u1', 'run_blog_interrupt')
const initialFailures = () => providerFailureProjection.forAuthority('run_L', 'msg_u1')
const advance = (state, identity, consecutiveFailureCount) => {
  const receipt = providerFailureProjection.applyFailure(identity, consecutiveFailureCount, state)
  assert.equal(receipt.ok, true, receipt.ok ? '' : receipt.error)
  return receipt.value
}
const failureState = (state) => providerFailureProjection.read(state)
const observeOwnerFailure = (state) => advance(state, OWNER, failureState(state).failures + 1)
const observeDuplicateOwnerFailure = (state) => {
  const duplicate = providerFailureProjection.applyFailure(OWNER, failureState(state).failures, state)
  assert.deepEqual(duplicate, { ok: false, error: 'AlreadyObserved' })
  return state
}
const interleavings = [
  ['owner', 'blogger-residue', 'join'],
  ['owner', 'join', 'blogger-residue'],
  ['blogger-residue', 'owner', 'join'],
  ['blogger-residue', 'join', 'owner'],
  ['join', 'owner', 'blogger-residue'],
  ['join', 'blogger-residue', 'owner'],
]
const applyObservedOwnerDecision = (state, observation) =>
  observation === 'owner' ? observeOwnerFailure(state) : state
const linkedHandle = () => {
  const linked = handles.apply(handles.empty(), {
    op: 'link',
    handle: 'agent:h1',
    child: 'ses_child',
    agent: 'coder',
    role: 'Coder',
  })
  assert.equal(linked.ok, true, linked.ok ? '' : JSON.stringify(linked.error))
  return linked.state
}
const applyHandle = (state, command) => handles.apply(state, { handle: 'agent:h1', ...command })

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_owner_failure_blogger_interrupt_interleavings_at_most_once', () => {
  for (const observations of interleavings) {
    const state = observations.reduce(applyObservedOwnerDecision, initialFailures())
    assert.deepEqual(
      (({ failures, dedupeKeys }) => ({ failures, dedupeKeys }))(failureState(state)),
      { failures: 1, dedupeKeys: 1 },
    )
  }
})
test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_owner_failure_alone_still_exactly_once_under_duplicate_observation', () => {
  const first = observeOwnerFailure(initialFailures())
  const afterDuplicate = observeDuplicateOwnerFailure(first)
  assert.deepEqual(failureState(afterDuplicate), failureState(first))
})
test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_counterfactual_blogger_advance_on_owner_would_double_count', () => {
  const ownerAdvanced = observeOwnerFailure(initialFailures())
  const doubleCounted = advance(ownerAdvanced, BLOGGER, failureState(ownerAdvanced).failures + 1)
  assert.deepEqual(
    (({ failures, dedupeKeys }) => ({ failures, dedupeKeys }))(failureState(doubleCounted)),
    { failures: 2, dedupeKeys: 2 },
  )
})
test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_join_guard_handle_complete_retire_exactly_once_projection', () => {
  const completed = applyHandle(linkedHandle(), { op: 'complete', kind: 'Terminal' })
  assert.equal(completed.ok, true, completed.ok ? '' : JSON.stringify(completed.error))
  assert.equal(handles.read(completed.state, 'agent:h1').lifecycle, 'CompletedAwaitingJoin')
  assert.equal(handles.views(completed.state).joinable.length, 1)

  const retired = applyHandle(completed.state, { op: 'retire' })
  assert.equal(retired.ok, true, retired.ok ? '' : JSON.stringify(retired.error))
  assert.equal(handles.read(retired.state, 'agent:h1').lifecycle, 'Retired')
  assert.equal(handles.views(retired.state).joinable.length, 0)
})
test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_join_guard_fold_absorbs_duplicate_complete_and_retire', () => {
  const completed = applyHandle(linkedHandle(), { op: 'complete', kind: 'Terminal' })
  assert.equal(completed.ok, true, completed.ok ? '' : JSON.stringify(completed.error))

  const duplicateCompletion = applyHandle(completed.state, { op: 'complete', kind: 'Terminal' })
  assert.deepEqual(duplicateCompletion.error, { kind: 'TransitionRejected', reason: 'AlreadyCompleted' })
  assert.equal(handles.read(completed.state, 'agent:h1').lifecycle, 'CompletedAwaitingJoin')

  const retired = applyHandle(completed.state, { op: 'retire' })
  assert.equal(retired.ok, true, retired.ok ? '' : JSON.stringify(retired.error))
  const duplicateRetirement = applyHandle(retired.state, { op: 'retire' })
  assert.deepEqual(duplicateRetirement.error, { kind: 'TransitionRejected', reason: 'HandleIsRetired' })
  assert.equal(handles.read(retired.state, 'agent:h1').lifecycle, 'Retired')
})
test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_single_retry_budget_twelfth_consecutive_failure_is_terminal', () => {
  assert.equal(budget.defaultBudget, 12)
  let failures = budget.initial
  for (let attempt = 1; attempt <= 11; attempt += 1) {
    failures = budget.recordFailure(failures)
    assert.equal(budget.verdict(budget.defaultBudget, failures), 'MayRetry')
  }
  assert.deepEqual(budget.read(failures), { failures: 11 })
  failures = budget.recordFailure(failures)
  assert.equal(budget.verdict(budget.defaultBudget, failures), 'Exhausted')

  let projection = initialFailures()
  for (let attempt = 1; attempt <= 11; attempt += 1) {
    const identity = budget.attemptIdentity('ses_mgr', 'run_L', 'msg_u1', `run_owner_${attempt}`)
    projection = advance(projection, identity, attempt)
    assert.equal(providerFailureProjection.mayRetry(budget.defaultBudget, projection), true)
  }
  const terminal = providerFailureProjection.applyExhausted(projection)
  assert.equal(providerFailureProjection.mayRetry(budget.defaultBudget, terminal), false)
  assert.equal(providerFailureProjection.read(terminal).exhausted, true)

  const recovered = providerFailureProjection.recordSuccess(terminal)
  assert.deepEqual(
    ((state) => ({ failures: state.failures, dedupeKeys: state.dedupeKeys }))(
      providerFailureProjection.read(recovered),
    ),
    { failures: 0, dedupeKeys: 0 },
  )
})
}
