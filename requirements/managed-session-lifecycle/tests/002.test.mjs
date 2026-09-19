import assert from 'node:assert/strict'
import test from 'node:test'
import * as SatelliteSurface from '../../../dist/OpenCode/Host/SatelliteSurface.js'



test('WHAT[managed-session-lifecycle-002] HOST_014_concurrent_first_ensure_is_single_flight_and_creates_one_child', async () => {
  const observed = await SatelliteSurface.SatelliteSurface_concurrent()
  assert.deepEqual(observed.created, ['created-1'])
  assert.deepEqual(observed.children, ['created-1', 'created-1'])
})
