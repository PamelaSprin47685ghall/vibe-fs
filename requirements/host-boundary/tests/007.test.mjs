import assert from 'node:assert/strict'
import test from 'node:test'
import * as cap from '../../../dist/OpenCode/Host/CapabilityObservationSurface.js'

test('WHAT[HOST-BOUNDARY-007] HOST_006_prevention_requires_compaction_settings_off_and_autocontinue_off', () => {
  const cfg = cap.validateCompactionSettings({ autoCompaction: false, autoContinue: false })
  assert.equal(cfg.ok, true)
})

test('WHAT[HOST-BOUNDARY-007] HOST_006_first_turn_probe_is_the_only_startup_verdict', () => {
  assert.equal(cap.startupVerdictFromProbe({ probeResult: 'clean' }), 'Accepted')
})

test('WHAT[HOST-BOUNDARY-007] HOST_006_containment_folds_observation_and_reanchors_newest_unhandled_once', () => {
  const res = cap.containmentReanchor({ unhandled: ['e1', 'e2'] })
  assert.equal(res.reanchoredCount, 1)
})
