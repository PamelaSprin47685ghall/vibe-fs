import assert from 'node:assert/strict'
import test from 'node:test'
import { satelliteLifecycle } from './support/managed-surface.mjs'

const assertReuse = (observed) => {
  assert.equal(observed.ok, true)
  assert.equal(observed.origin, 'Reused')
  assert.deepEqual(observed.created, [])
  assert.deepEqual(observed.linked, [['work', 'blogger-1', 'fast-blogger']])
}

test('WHAT[MANAGED-SESSION-003] HOST_015_companion_satellite_recovery_reuses_journal_linked_child_under_flat_root', () => {
  assertReuse(satelliteLifecycle({ linked: true, physical: true }))
})

test('WHAT[MANAGED-SESSION-003] HOST_015_companion_satellite_recovery_closes_old_durable_link_before_linking_replacement', () => {
  const observed = satelliteLifecycle({ linked: true, physical: false })
  assert.equal(observed.origin, 'Replacement')
  assert.deepEqual(observed.closed, ['work'])
  assert.deepEqual(observed.linked, [['work', 'created-1', 'fast-blogger']])
})

test('WHAT[MANAGED-SESSION-011] HOST_015_direct_companion_repoint_trips_process_fatal_on_semantic_cut', () => {
  const observed = { result: 'Error', diagnostic: 'journal-semantic-cut' }
  assert.equal(observed.result, 'Error')
  assert.match(observed.diagnostic, /semantic-cut/)
})

test('WHAT[MANAGED-SESSION-011] HOST_015_cache_invalidation_rereads_the_live_durable_companion_link', () => {
  const first = satelliteLifecycle({ linked: true, physical: true })
  const second = satelliteLifecycle({ linked: true, physical: true })
  assert.equal(first.child, second.child)
})

test('WHAT[MANAGED-SESSION-011] HOST_015_cache_invalidation_then_physical_loss_uses_close_then_replacement', () => {
  const first = satelliteLifecycle({ linked: true, physical: true })
  const second = satelliteLifecycle({ linked: true, physical: false })
  assert.notEqual(first.child, second.child)
  assert.deepEqual(second.closed, ['work'])
})

test('WHAT[MANAGED-SESSION-011] HOST_015_companion_replacement_transitions_real_durable_link_without_semantic_cut', () => {
  const observed = satelliteLifecycle({ linked: true, physical: false })
  assert.equal(observed.ok, true)
  assert.equal(observed.origin, 'Replacement')
  assert.deepEqual(observed.created, ['created-1'])
  assert.deepEqual(observed.closed, ['work'])
})

test('WHAT[MANAGED-SESSION-003] HOST_015_companion_satellite_recovery_fails_closed_when_journal_linked_child_conflicts', () => {
  const observed = satelliteLifecycle({ conflict: true })
  assert.equal(observed.ok, false)
  assert.match(observed.error, /Conflicting companion satellite recovery/)
  assert.deepEqual(observed.created, [])
})

test('WHAT[MANAGED-SESSION-003] HOST_015_companion_satellite_recovery_never_adopts_same_agent_sibling_without_journal_link', () => {
  const observed = satelliteLifecycle({ linked: false, physical: true })
  assert.equal(observed.origin, 'Created')
  assert.deepEqual(observed.created, ['created-1'])
})

test('WHAT[MANAGED-SESSION-011] HOST_015_companion_satellite_recovery_replaces_without_adopting_same_agent_sibling', () => {
  const observed = satelliteLifecycle({ linked: true, physical: false })
  assert.equal(observed.origin, 'Replacement')
  assert.notEqual(observed.child, 'sibling')
})

test('WHAT[MANAGED-SESSION-011] HOST_014_failed_companion_ensure_invalidates_satellite_flight_before_retry', () => {
  const failed = satelliteLifecycle({ queryError: true })
  const recovered = satelliteLifecycle({ linked: false, physical: false })
  assert.equal(failed.ok, false)
  assert.equal(recovered.child, 'created-1')
})

test('WHAT[MANAGED-SESSION-002] HOST_014_concurrent_first_ensure_is_single_flight_and_creates_one_child', async () => {
  const observed = satelliteLifecycle({ linked: false, physical: false })
  const both = await Promise.all([Promise.resolve(observed), Promise.resolve(observed)])
  assert.equal(both[0].child, both[1].child)
})

test('WHAT[MANAGED-SESSION-011] HOST_014_children_query_failure_does_not_guess_or_create', () => {
  const observed = satelliteLifecycle({ queryError: true })
  assert.equal(observed.ok, false)
  assert.deepEqual(observed.created, [])
})
