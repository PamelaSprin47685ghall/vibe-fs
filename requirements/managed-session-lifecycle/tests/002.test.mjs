import assert from 'node:assert/strict'
import test from 'node:test'
import * as satellite from '../../../dist/Execution/Session/SatelliteRuntimeSurface.js'
import * as satelliteSurface from '../../../dist/OpenCode/Host/SatelliteSurface.js'

test('WHAT[MANAGED-SESSION-002] HOST_014_concurrent_first_ensure_is_single_flight_and_creates_one_child', async () => {
  const r = await satellite.testSingleFlightEnsure()
  assert.equal(r.childrenCreated, 1)
})
