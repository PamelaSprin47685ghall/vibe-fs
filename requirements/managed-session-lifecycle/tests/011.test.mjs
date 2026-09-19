import assert from 'node:assert/strict'
import test from 'node:test'
import * as SatelliteSurface from '../../../dist/OpenCode/Host/SatelliteSurface.js'



test('WHAT[managed-session-lifecycle-011] HOST_015_missing_restored_child_closes_then_links_replacement', async () => {
  const observed = await SatelliteSurface.SatelliteSurface_scenario(true, false, false, false)
  assert.equal(observed.ok, true)
  assert.equal(observed.origin, 'Replacement')
  assert.deepEqual(observed.created, ['created-1'])
  assert.deepEqual(observed.closed, ['work'])
  assert.deepEqual(observed.linked, [['work', 'created-1', 'blogger']])
})

test('WHAT[managed-session-lifecycle-011] HOST_014_children_query_failure_does_not_guess_or_create', async () => {
  const observed = await SatelliteSurface.SatelliteSurface_scenario(false, false, false, true)
  assert.equal(observed.ok, false)
  assert.match(observed.error, /Cannot recover companion satellite/)
  assert.deepEqual(observed.created, [])
})
