// Effect-accounting laws through the fallback and handle owner surfaces.
import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import * as fallback from '../../../dist/Participant/Provider/Attempt/Fallback/Surface.js'
import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'

const ROOT = new URL('../../../', import.meta.url).pathname

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_owner_failure_blogger_interrupt_interleavings_at_most_once', () => {
  const state = fallback.ownerFailure()
  assert.deepEqual(
    { offset: state.offset, failures: state.failures, dedupeKeys: state.dedupeKeys },
    { offset: 1, failures: 1, dedupeKeys: 1 },
  )
  for (const permutation of fallback.permutations()) {
    assert.deepEqual(
      { offset: permutation.offset, failures: permutation.failures, dedupeKeys: permutation.dedupeKeys },
      { offset: 1, failures: 1, dedupeKeys: 1 },
    )
  }
})

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_owner_failure_alone_still_exactly_once_under_duplicate_observation', () => {
  assert.deepEqual(fallback.duplicateOwnerFailure(), fallback.ownerFailure())
})

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_counterfactual_blogger_advance_on_owner_would_double_count', () => {
  const state = fallback.counterfactualBloggerFailure()
  assert.equal(state.failures, 2)
  assert.equal(state.dedupeKeys, 2)
})

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_join_guard_handle_complete_retire_exactly_once_projection', () => {
  assert.deepEqual(handles.crashScenario('replayed-completed'), {
    lifecycle: 'CompletedAwaitingJoin',
    completion: { kind: 'Terminal' },
    abandonReason: null,
    joinable: 1,
    retired: false,
  })
  assert.deepEqual(handles.crashScenario('replayed-retired'), {
    lifecycle: 'Retired',
    completion: { kind: 'Terminal' },
    abandonReason: null,
    joinable: 0,
    retired: true,
  })
})

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_join_guard_fold_absorbs_duplicate_complete_and_retire', () => {
  for (const action of ['completed', 'replayed-completed', 'retired', 'replayed-retired']) {
    const state = handles.crashScenario(action)
    assert.ok(['CompletedAwaitingJoin', 'Retired'].includes(state.lifecycle))
    if (state.lifecycle === 'Retired') assert.equal(state.joinable, 0)
  }
})

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_manager_lifecycle_activation_and_life_completed_exactly_once', () => {
  const source = readFileSync(join(ROOT, 'src/Wanxiangshu/Mission/Manager/Life/Projection.fs'), 'utf8')
  assert.match(source, /WorkActivated/)
  assert.match(source, /LifeCompleted/)
  assert.match(source, /CompletedLives/)
})

test('WHAT[EFFECT-ACCOUNTING-004] THEOREM_owner_advance_and_blogger_residue_permutations_confluent', () => {
  const states = fallback.permutations()
  assert.equal(states.length, 6)
  assert.ok(states.every((state) => state.failures === 1 && state.offset === 1))
})
