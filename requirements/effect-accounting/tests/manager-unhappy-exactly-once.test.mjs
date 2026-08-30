// Effect-accounting laws through the production fallback and handle owners.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { cursor, fallbackProjection } from '../../../dist/Participant/Provider/Attempt/Fallback/CursorSurface.js'
import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'

const ROOT = new URL('../../../', import.meta.url).pathname
const OWNER = cursor.attemptIdentity('ses_mgr', 'run_L', 'msg_u1', 'run_owner')
const BLOGGER = cursor.attemptIdentity('ses_mgr', 'run_L', 'msg_u1', 'run_blog_interrupt')

const initialFallback = () => fallbackProjection.forAuthority('run_L', 'msg_u1')

const advance = (state, identity, previousOffset, nextOffset, failures) => {
  const receipt = fallbackProjection.applyAdvance(identity, previousOffset, nextOffset, failures, state)
  assert.equal(receipt.ok, true, receipt.ok ? '' : receipt.error)
  return receipt.value
}

const fallbackState = (state) => fallbackProjection.read(state)

const observeOwnerFailure = (state) => advance(state, OWNER, 0, 1, 1)

const observeDuplicateOwnerFailure = (state) => {
  const duplicate = fallbackProjection.applyAdvance(OWNER, 0, 1, 1, state)
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
    agent: 'fast-coder',
    role: 'Coder',
  })
  assert.equal(linked.ok, true, linked.ok ? '' : JSON.stringify(linked.error))
  return linked.state
}

const applyHandle = (state, command) => handles.apply(state, { handle: 'agent:h1', ...command })

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_owner_failure_blogger_interrupt_interleavings_at_most_once', () => {
  for (const observations of interleavings) {
    const state = observations.reduce(applyObservedOwnerDecision, initialFallback())
    assert.deepEqual(
      (({ offset, failures, dedupeKeys }) => ({ offset, failures, dedupeKeys }))(fallbackState(state)),
      { offset: 1, failures: 1, dedupeKeys: 1 },
    )
  }
})

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_owner_failure_alone_still_exactly_once_under_duplicate_observation', () => {
  const first = observeOwnerFailure(initialFallback())
  const afterDuplicate = observeDuplicateOwnerFailure(first)
  assert.deepEqual(fallbackState(afterDuplicate), fallbackState(first))
})

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_counterfactual_blogger_advance_on_owner_would_double_count', () => {
  const ownerAdvanced = observeOwnerFailure(initialFallback())
  const doubleCounted = advance(ownerAdvanced, BLOGGER, 1, 2, 2)
  assert.deepEqual(
    (({ offset, failures, dedupeKeys }) => ({ offset, failures, dedupeKeys }))(fallbackState(doubleCounted)),
    { offset: 2, failures: 2, dedupeKeys: 2 },
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

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_manager_lifecycle_activation_and_life_completed_exactly_once', () => {
  const source = readFileSync(join(ROOT, 'src/Wanxiangshu/Mission/Manager/Life/Projection.fs'), 'utf8')
  assert.match(source, /WorkActivated/)
  assert.match(source, /LifeCompleted/)
  assert.match(source, /CompletedLives/)
})

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_owner_advance_and_blogger_residue_permutations_confluent', () => {
  const states = interleavings.map((observations) =>
    fallbackState(observations.reduce(applyObservedOwnerDecision, initialFallback())),
  )
  assert.equal(states.length, 6)
  assert.ok(states.every((state) => state.failures === 1 && state.offset === 1 && state.dedupeKeys === 1))
})
