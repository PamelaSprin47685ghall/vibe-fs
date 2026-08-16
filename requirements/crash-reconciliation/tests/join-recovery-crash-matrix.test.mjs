/**
 * P0-RECOVERY-JOIN-001 § crash matrix (unit fold): crash points after Aborted /
 * before proof append / after HandleCompleted / after HandleRetired.
 *
 * No fake durable aborted; no double completion.
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  completionKind,
  envelope,
  fact,
  fold,
  handleId,
  handleProjection,
  roles,
  sessionId,
  stream,
} from '../../verification-system/tests/support/domain.mjs'
import { HandleOwnership } from '../../../dist/Composition/Durable/Fact.js'

const PARENT = sessionId('ses_crash_p')
const CHILD = sessionId('ses_crash_c')
const HANDLE = handleId.agent('h-crash')

const handleFact = {
  linked: fact('HandleLinked', {
    ParentSessionId: PARENT,
    ChildSessionId: CHILD,
    Handle: HANDLE,
    TargetAgent: 'fast-coder',
    CanonicalRole: roles.of('Coder'),
    Ownership: HandleOwnership.DurableParentHandle,
  }),
  completed: fact('HandleCompleted', {
    ParentSessionId: PARENT,
    Handle: HANDLE,
    Kind: completionKind.of('Terminal'),
    CompletionRef: undefined,
    CompletionDigest: undefined,
  }),
  retired: fact('HandleRetired', { ParentSessionId: PARENT, Handle: HANDLE }),
}

const foldFacts = (facts) =>
  fold.apply(
    fold.empty,
    facts.map((value, index) =>
      envelope({ seq: index + 1, stream: stream.session(PARENT), fact: value }),
    ),
  )

const handlesOf = (folded) => {
  assert.equal(folded.ok, true, folded.ok ? '' : JSON.stringify(folded.error))
  return fold.session(folded.value, 'ses_crash_p').Handles
}

const lifecycleOf = (handles) =>
  handleProjection.read(handleProjection.tryFind(HANDLE, handles)).lifecycle

// 1. AbortedObserved after link, no durable fact → Active
test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_crash_after_aborted_observed_stays_active', () => {
  // Aborted is never a journal fact; crash after observation leaves only link.
  const handles = handlesOf(foldFacts([handleFact.linked]))
  assert.equal(lifecycleOf(handles), 'Active')
  assert.equal(handleProjection.joinable(handles).length, 0)
  assert.equal(handleProjection.isAbandoned(HANDLE, handles), false)
  assert.equal(handleProjection.isRetired(HANDLE, handles), false)
})

// 2. Proof minted in memory but append not yet committed → no HandleCompleted
test('WHAT[CRASH-005] P0_RECOVERY_JOIN_001_crash_before_handle_completed_append_has_no_completion', () => {
  const handles = handlesOf(foldFacts([handleFact.linked]))
  assert.equal(lifecycleOf(handles), 'Active')
  // Replaying the same partial journal still has zero completions.
  const again = handlesOf(foldFacts([handleFact.linked]))
  assert.equal(lifecycleOf(again), 'Active')
  assert.equal(handleProjection.joinable(again).length, 0)
})

// 3. HandleCompleted after append, consume not yet → CompletedAwaitingJoin; replay no double-complete
test('WHAT[CRASH-002] P0_RECOVERY_JOIN_001_crash_after_completed_before_consume_is_awaiting_join', () => {
  const handles = handlesOf(foldFacts([handleFact.linked, handleFact.completed]))
  assert.equal(lifecycleOf(handles), 'CompletedAwaitingJoin')
  assert.equal(handleProjection.joinable(handles).length, 1)

  // Replay with duplicate HandleCompleted is absorbed (no second cell).
  const replayed = handlesOf(
    foldFacts([handleFact.linked, handleFact.completed, handleFact.completed]),
  )
  assert.equal(lifecycleOf(replayed), 'CompletedAwaitingJoin')
  assert.equal(handleProjection.joinable(replayed).length, 1)
})

// 4. HandleRetired after consume → Retired; retire idempotent on replay
test('WHAT[CRASH-012] P0_RECOVERY_JOIN_001_crash_after_retired_is_idempotent', () => {
  const handles = handlesOf(
    foldFacts([handleFact.linked, handleFact.completed, handleFact.retired]),
  )
  assert.equal(lifecycleOf(handles), 'Retired')
  assert.equal(handleProjection.joinable(handles).length, 0)

  const replayed = handlesOf(
    foldFacts([
      handleFact.linked,
      handleFact.completed,
      handleFact.retired,
      handleFact.retired,
      handleFact.completed,
    ]),
  )
  assert.equal(lifecycleOf(replayed), 'Retired')
  assert.equal(handleProjection.isRetired(HANDLE, replayed), true)
})

test('WHAT[CRASH-009] P0_RECOVERY_JOIN_001_crash_matrix_no_aborted_durable_fact', () => {
  // Journal only carries link/complete/retire/abandon — never an "aborted" fact.
  // Partial recovery after aborts is indistinguishable from Active link-only state.
  const handles = handlesOf(foldFacts([handleFact.linked]))
  const state = handleProjection.read(handleProjection.tryFind(HANDLE, handles))
  assert.equal(state.lifecycle, 'Active')
  assert.equal(state.completion, undefined)
  assert.equal(state.abandonReason, undefined)
})
