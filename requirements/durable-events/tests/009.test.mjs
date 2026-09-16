import assert from 'node:assert/strict'
import test from 'node:test'
import * as cut from '../../../dist/Persistence/ShockCutSurface.js'

test('WHAT[DURABLE-EVENTS-009] shock_cut_source_has_no_legacy_shape_detection_migration_or_reset', () => {
  assert.equal(cut.hasLegacyDetection(), false)
})
