import assert from 'node:assert/strict'
import test from 'node:test'
import * as SatelliteSurface from '../../../dist/Execution/Session/Attachment/SatelliteSurface.js'

test('WHAT[MANAGED-SESSION-003] HOST_015_companion_reuses_exact_journal_linked_physical_child', async () => {
  const observed = await SatelliteSurface.scenario(true, true, false, false)
  assert.equal(observed.ok, true)
  assert.equal(observed.origin, 'Reused')
  assert.equal(observed.child, 'blogger-1')
  assert.deepEqual(observed.created, [])
  assert.deepEqual(observed.linked, [['work', 'blogger-1', 'fast-blogger']])
})

test('WHAT[MANAGED-SESSION-011] HOST_015_missing_restored_child_closes_then_links_replacement', async () => {
  const observed = await SatelliteSurface.scenario(true, false, false, false)
  assert.equal(observed.ok, true)
  assert.equal(observed.origin, 'Replacement')
  assert.deepEqual(observed.created, ['created-1'])
  assert.deepEqual(observed.closed, ['work'])
  assert.deepEqual(observed.linked, [['work', 'created-1', 'fast-blogger']])
})

test('WHAT[MANAGED-SESSION-003] HOST_015_conflicting_restored_child_fails_closed', async () => {
  const observed = await SatelliteSurface.scenario(true, true, true, false)
  assert.equal(observed.ok, false)
  assert.match(observed.error, /Conflicting companion satellite recovery/)
  assert.deepEqual(observed.created, [])
  assert.deepEqual(observed.linked, [])
})

test('WHAT[MANAGED-SESSION-003] HOST_015_without_durable_link_never_adopts_matching_sibling', async () => {
  const observed = await SatelliteSurface.scenario(false, true, false, false)
  assert.equal(observed.ok, true)
  assert.equal(observed.origin, 'Created')
  assert.equal(observed.child, 'created-1')
})

test('WHAT[MANAGED-SESSION-011] HOST_014_children_query_failure_does_not_guess_or_create', async () => {
  const observed = await SatelliteSurface.scenario(false, false, false, true)
  assert.equal(observed.ok, false)
  assert.match(observed.error, /Cannot recover companion satellite/)
  assert.deepEqual(observed.created, [])
})

test('WHAT[MANAGED-SESSION-002] HOST_014_concurrent_first_ensure_is_single_flight', async () => {
  const observed = await SatelliteSurface.concurrent()
  assert.deepEqual(observed.created, ['created-1'])
  assert.deepEqual(observed.children, ['created-1', 'created-1'])
})
